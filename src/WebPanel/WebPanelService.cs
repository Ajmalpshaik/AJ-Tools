#region Metadata
/*
 * Tool Name     : Web Panel (HTTP server)
 * File Name     : WebPanelService.cs
 * Purpose       : Local HTTP server that serves the AJ Tools Web Panel page to a browser on this
 *                 machine and turns its button clicks into tool runs on the live Revit document,
 *                 by handing a tool id to WebPanelToolRunner. Started and stopped by the "Web
 *                 Panel" ribbon button (CmdToggleWebPanel).
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
 * Dependencies  : System.Net (HttpListener), Newtonsoft.Json, WebPanelToolRunner
 *
 * Input         : HTTP requests on http://localhost:<port>/ only.
 * Output        : The panel page, a tool list, and tool results as JSON. Model changes only ever
 *                 happen through WebPanelToolRunner.
 *
 * Notes         :
 * - HttpListener is bound to the explicit "localhost" prefix, never the "+" or "*" wildcard.
 *   MEASURED on Ajmal's machine 2026-08-12 as a standard non-admin user: "http://localhost:5599/"
 *   starts fine, while "http://+:5599/" throws "Access is denied". So this needs NO admin rights and
 *   NO one-time `netsh http add urlacl` reservation. (McpBridgeService's own header note says an HTTP
 *   loopback listener needs admin or a URL ACL - that is true only for the wildcard form, and has
 *   been corrected there.) A localhost-only prefix is also not reachable from another machine, so it
 *   raises no Windows Firewall prompt.
 * - The page is served BY this listener, so the browser sees one single origin. That avoids the
 *   mixed-content and CORS problems a page hosted on an https website would hit trying to reach
 *   http://localhost.
 * - Two defences, because either alone has a hole:
 *     (a) Per-session token, generated at Start() and injected into the page it serves. Every /api
 *         call must present it.
 *     (b) Origin check. A browser always sends Origin on a cross-origin POST, so a hostile page open
 *         in another tab is rejected outright even before the token is considered - which matters
 *         because that page cannot read our token (CORS blocks it reading the response) but could
 *         otherwise still fire blind requests.
 *   Neither stops another program running as Ajmal himself; nothing here can, and the same is true
 *   of the existing AJ AI named pipe. The threat this closes is a web page, not a local process.
 * - Port is picked from a small range rather than fixed, so two Revit versions open at once each get
 *   their own. The chosen port and token are written to a discovery file next to the AJ AI one.
 * - If Revit is showing a modal dialog, ExternalEvent cannot fire and the run will time out. That is
 *   reported plainly to the browser rather than left to hang.
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

namespace AJTools.WebPanel
{
    public class WebPanelService
    {
        // Deliberately an uncommon, unregistered range - low chance of colliding with anything else
        // the machine already runs, and small enough that several Revit sessions still fit.
        private const int FirstPort = 48210;
        private const int LastPort = 48229;

        // Long enough for a full-model collector on a heavy project, short enough that a browser tab
        // never looks frozen forever if Revit is sitting behind a modal dialog.
        private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(90);

        private readonly WebPanelToolRunner _runner;

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _acceptLoop;

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }
        public string Token { get; private set; }

        public string Url
        {
            get { return "http://localhost:" + Port + "/"; }
        }

        public WebPanelService(WebPanelToolRunner runner)
        {
            _runner = runner;
        }

        private static string DiscoveryFilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AJTools", "webpanel.json");
            }
        }

        #region Start / Stop

        public bool Start(out string errorMessage)
        {
            if (IsRunning)
            {
                errorMessage = null;
                return true;
            }

            HttpListener listener = null;
            try
            {
                listener = TryBindFreePort();
                if (listener == null)
                {
                    errorMessage = "Could not open a local port for the Web Panel (tried " +
                                   FirstPort + "-" + LastPort + "). Close another Revit session and try again.";
                    return false;
                }

                _listener = listener;
                Token = GenerateToken();
                WriteDiscoveryFile();

                _cts = new CancellationTokenSource();
                IsRunning = true;
                _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                IsRunning = false;

                // Same leak guard as McpBridgeService.Start: if binding succeeded but something after
                // it (writing the discovery file) threw, nothing else would ever close the listener.
                try { if (listener != null && listener.IsListening) listener.Stop(); } catch { }
                try { if (listener != null) listener.Close(); } catch { }
                _listener = null;

                errorMessage = "Could not start the Web Panel: " + ex.Message;
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

            DeleteDiscoveryFile();
        }

        /// <summary>
        /// Walks the port range and returns the first listener that actually starts. Binding is the
        /// only reliable free-port test - checking a port then binding it is a race.
        /// </summary>
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

        #region Request loop

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
                    // Listener stopped, or a single bad connection. If we are shutting down, leave;
                    // otherwise keep serving - one dropped request must not kill the panel.
                    if (token.IsCancellationRequested || !IsRunning) break;
                    continue;
                }

                // Each request on its own task so a slow tool run never blocks the next click.
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
                    WriteHtml(context, WebPanelPage.Build(Token));
                    return;
                }

                if (path == "/api/tools")
                {
                    if (!IsAuthorised(context)) return;

                    var list = new List<object>();
                    foreach (var tool in WebPanelToolRunner.Tools)
                    {
                        if (tool.Id == "context") continue; // internal probe, not a button
                        list.Add(new { id = tool.Id, name = tool.Name, panel = tool.Panel, description = tool.Description });
                    }

                    WriteJson(context, 200, new { success = true, tools = list });
                    return;
                }

                if (path == "/api/run" || path == "/api/context")
                {
                    if (!IsAuthorised(context)) return;

                    string toolId = path == "/api/context" ? "context" : ReadToolId(context);
                    if (string.IsNullOrWhiteSpace(toolId))
                    {
                        WriteJson(context, 400, new { success = false, message = "No tool was named in the request." });
                        return;
                    }

                    var runTask = _runner.RunAsync(toolId);
                    var finished = await Task.WhenAny(runTask, Task.Delay(RunTimeout)).ConfigureAwait(false);

                    if (finished != runTask)
                    {
                        WriteJson(context, 200, new
                        {
                            success = false,
                            message = "Revit did not respond in time. It is usually showing a dialog that needs " +
                                      "an answer, or is busy with another command - check the Revit window."
                        });
                        return;
                    }

                    var result = await runTask.ConfigureAwait(false);
                    WriteJson(context, 200, new { success = result.Success, message = result.Message });
                    return;
                }

                WriteJson(context, 404, new { success = false, message = "Unknown address." });
            }
            catch (Exception ex)
            {
                try { WriteJson(context, 500, new { success = false, message = ex.Message }); } catch { }
            }
        }

        private string ReadToolId(HttpListenerContext context)
        {
            try
            {
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(body)) return null;

                    var request = JsonConvert.DeserializeObject<WebPanelRunRequest>(body);
                    return request == null ? null : request.ToolId;
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Rejects a request whose Origin header names a different site. A browser always sends
        /// Origin on a cross-origin POST, so this stops a hostile page in another tab firing blind
        /// requests at the panel. A missing Origin (our own page's same-origin fetch, or a direct
        /// address-bar visit) is fine and falls through to the token check.
        /// </summary>
        private bool IsOriginAllowed(HttpListenerRequest request)
        {
            string origin = request.Headers["Origin"];
            if (string.IsNullOrEmpty(origin)) return true;

            return string.Equals(origin.TrimEnd('/'), "http://localhost:" + Port, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Writes the 401 itself when the token is wrong, so callers can just return.</summary>
        private bool IsAuthorised(HttpListenerContext context)
        {
            string supplied = context.Request.Headers["X-AJ-Token"];
            if (string.IsNullOrEmpty(supplied))
                supplied = context.Request.QueryString["token"];

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
            // (Revit 2025+) reports obsolete via SYSLIB0023 - same call McpBridgeService uses.
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            // URL/HTML safe: this value gets embedded in the served page and sent back in a header.
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        #endregion

        #region Responses and discovery file

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

        private void WriteDiscoveryFile()
        {
            string dir = Path.GetDirectoryName(DiscoveryFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var info = new { port = Port, token = Token, url = Url };
            File.WriteAllText(DiscoveryFilePath, JsonConvert.SerializeObject(info));
        }

        private static void DeleteDiscoveryFile()
        {
            try
            {
                if (File.Exists(DiscoveryFilePath)) File.Delete(DiscoveryFilePath);
            }
            catch { /* best-effort cleanup */ }
        }

        #endregion
    }

    public class WebPanelRunRequest
    {
        public string ToolId { get; set; }
    }
}
