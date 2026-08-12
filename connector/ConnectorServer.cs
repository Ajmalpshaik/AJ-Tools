#region Metadata
/*
 * Tool Name     : AJ Tools Connector (HTTP server)
 * File Name     : ConnectorServer.cs
 * Purpose       : Serves the connector page to a browser on this machine and turns its button
 *                 clicks into tool runs. Tools come from ToolCatalogue (signed, fetched from the
 *                 tool source); nothing runnable is compiled into this add-in.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-12
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System.Net (HttpListener), Newtonsoft.Json, ToolCatalogue, ScriptRunner
 *
 * Input         : HTTP requests on http://localhost:<port>/ only.
 * Output        : The page, the verified tool list, and tool results as JSON.
 *
 * Notes         :
 * - Bound to the explicit "localhost" prefix, never the "+"/"*" wildcard. MEASURED 2026-08-12 on a
 *   standard non-admin Windows account: "http://localhost:<port>/" starts fine while "http://+:<port>/"
 *   throws "Access is denied". So this needs no admin rights and no `netsh http add urlacl`, and it
 *   raises no firewall prompt because it is unreachable from another machine.
 * - Port range 48230-48249, deliberately DIFFERENT from AJ Tools' own Web Panel (48210-48229), so a
 *   machine with both installed keeps two obviously distinct addresses instead of a confusing race.
 * - Two defences, because each alone has a hole: a per-session token injected into the served page,
 *   and an Origin check. A hostile page in another tab cannot read the token (CORS stops it reading
 *   the response) but could otherwise fire blind requests - the Origin check refuses exactly that.
 *   Neither stops another program running as the same user; nothing can.
 * - The page is served BY this listener, so the browser sees one origin. That sidesteps the
 *   mixed-content and CORS problems an https website would hit reaching http://localhost.
 * - RUNS ONLY WHAT THE CATALOGUE ACCEPTED. /api/run looks the id up in the catalogue, which only ever
 *   holds tools whose signature verified. An id that is not there is simply unknown - there is no
 *   path from an HTTP request to arbitrary code.
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AJToolsConnector
{
    public class ConnectorServer
    {
        private const int FirstPort = 48230;
        private const int LastPort = 48249;

        private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(90);

        private readonly ScriptRunner _runner;
        private readonly ToolCatalogue _catalogue;

        private HttpListener _listener;
        private CancellationTokenSource _cts;

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }
        public string Token { get; private set; }

        public string Url { get { return "http://localhost:" + Port + "/"; } }

        public ConnectorServer(ScriptRunner runner, ToolCatalogue catalogue)
        {
            _runner = runner;
            _catalogue = catalogue;
        }

        private static string SessionFilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AJTools", "connector-session.json");
            }
        }

        #region Start / Stop

        public bool Start(out string errorMessage)
        {
            if (IsRunning) { errorMessage = null; return true; }

            HttpListener listener = null;
            try
            {
                listener = TryBindFreePort();
                if (listener == null)
                {
                    errorMessage = "Could not open a local port for the connector (tried " +
                                   FirstPort + "-" + LastPort + ").";
                    return false;
                }

                _listener = listener;
                Token = GenerateToken();
                WriteSessionFile();

                _cts = new CancellationTokenSource();
                IsRunning = true;

                var token = _cts.Token;
                var ignoredLoop = Task.Run(() => AcceptLoopAsync(token));
                GC.KeepAlive(ignoredLoop);

                // Fetch the tool list straight away so the page has buttons the moment it opens.
                // Detached: a slow or unreachable source must never block the ribbon click.
                var ignoredRefresh = Task.Run(() => _catalogue.RefreshAsync());
                GC.KeepAlive(ignoredRefresh);

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                IsRunning = false;
                try { if (listener != null && listener.IsListening) listener.Stop(); } catch { }
                try { if (listener != null) listener.Close(); } catch { }
                _listener = null;

                errorMessage = "Could not start the connector: " + ex.Message;
                return false;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            if (_cts != null) _cts.Cancel();

            try { if (_listener != null && _listener.IsListening) _listener.Stop(); } catch { }
            try { if (_listener != null) _listener.Close(); } catch { }
            _listener = null;

            try { if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath); } catch { }
        }

        /// <summary>Binding is the only reliable free-port test - checking then binding is a race.</summary>
        private HttpListener TryBindFreePort()
        {
            for (int port = FirstPort; port <= LastPort; port++)
            {
                var candidate = new HttpListener();
                candidate.Prefixes.Add("http://localhost:" + port + "/");

                try
                {
                    candidate.Start();
                    Port = port;
                    return candidate;
                }
                catch
                {
                    try { candidate.Close(); } catch { }
                }
            }

            return null;
        }

        #endregion

        #region Requests

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    if (token.IsCancellationRequested || !IsRunning) break;
                    continue;
                }

                var handled = context;
                var ignored = Task.Run(() => HandleRequestAsync(handled));
                GC.KeepAlive(ignored);
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath.TrimEnd('/');
                if (path.Length == 0) path = "/";

                if (!IsOriginAllowed(context.Request))
                {
                    WriteJson(context, 403, new { success = false, message = "Blocked: request came from another site." });
                    return;
                }

                if (path == "/")
                {
                    WriteHtml(context, ConnectorPage.Build(Token));
                    return;
                }

                if (!IsAuthorised(context)) return;

                switch (path)
                {
                    case "/api/tools":
                        WriteToolList(context);
                        return;

                    case "/api/refresh":
                        var refresh = await _catalogue.RefreshAsync().ConfigureAwait(false);
                        WriteJson(context, 200, new
                        {
                            success = refresh.Success,
                            message = refresh.Message,
                            accepted = refresh.Accepted,
                            refused = refresh.Refused,
                            source = _catalogue.Source
                        });
                        return;

                    case "/api/context":
                        await RespondWithRun(context, _runner.RunContextAsync()).ConfigureAwait(false);
                        return;

                    case "/api/run":
                        string toolId = ReadToolId(context);
                        var tool = _catalogue.Find(toolId);

                        if (tool == null)
                        {
                            WriteJson(context, 404, new
                            {
                                success = false,
                                message = "That tool is not in the verified list. Try Refresh."
                            });
                            return;
                        }

                        await RespondWithRun(context, _runner.RunAsync(tool)).ConfigureAwait(false);
                        return;

                    default:
                        WriteJson(context, 404, new { success = false, message = "Unknown address." });
                        return;
                }
            }
            catch (Exception ex)
            {
                try { WriteJson(context, 500, new { success = false, message = ex.Message }); } catch { }
            }
        }

        private void WriteToolList(HttpListenerContext context)
        {
            var list = new List<object>();
            foreach (var tool in _catalogue.Tools)
                list.Add(new { id = tool.Id, name = tool.Name, panel = tool.Panel, description = tool.Description });

            WriteJson(context, 200, new { success = true, tools = list, source = _catalogue.Source });
        }

        private static async Task RespondWithRun(HttpListenerContext context, Task<ToolRunResult> runTask)
        {
            var finished = await Task.WhenAny(runTask, Task.Delay(RunTimeout)).ConfigureAwait(false);

            if (finished != runTask)
            {
                WriteJson(context, 200, new
                {
                    success = false,
                    message = "Revit did not respond in time. It is usually showing a dialog that needs an " +
                              "answer, or is busy with another command - check the Revit window."
                });
                return;
            }

            var result = await runTask.ConfigureAwait(false);
            WriteJson(context, 200, new { success = result.Success, message = result.Message });
        }

        private static string ReadToolId(HttpListenerContext context)
        {
            try
            {
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(body)) return null;

                    var request = JsonConvert.DeserializeObject<RunRequest>(body);
                    return request == null ? null : request.ToolId;
                }
            }
            catch { return null; }
        }

        private class RunRequest
        {
            [JsonProperty("toolId")] public string ToolId { get; set; }
        }

        #endregion

        #region Authentication

        private bool IsOriginAllowed(HttpListenerRequest request)
        {
            string origin = request.Headers["Origin"];
            if (string.IsNullOrEmpty(origin)) return true;

            return string.Equals(origin.TrimEnd('/'), "http://localhost:" + Port, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAuthorised(HttpListenerContext context)
        {
            string supplied = context.Request.Headers["X-AJ-Token"];
            if (string.IsNullOrEmpty(supplied)) supplied = context.Request.QueryString["token"];

            if (TokensMatch(supplied, Token)) return true;

            WriteJson(context, 401, new { success = false, message = "Not authorised for this Revit session." });
            return false;
        }

        private static bool TokensMatch(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string GenerateToken()
        {
            var bytes = new byte[24];

            // RandomNumberGenerator.Create() rather than RNGCryptoServiceProvider, which .NET 8
            // (Revit 2025+) reports obsolete via SYSLIB0023.
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        #endregion

        #region Responses

        private static void WriteHtml(HttpListenerContext context, string html)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(html);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        private static void WriteJson(HttpListenerContext context, int statusCode, object payload)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        private void WriteSessionFile()
        {
            string dir = Path.GetDirectoryName(SessionFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(SessionFilePath,
                JsonConvert.SerializeObject(new { port = Port, token = Token, url = Url }));
        }

        #endregion
    }
}
