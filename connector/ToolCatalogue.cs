#region Metadata
/*
 * Tool Name     : AJ Tools Connector (tool catalogue)
 * File Name     : ToolCatalogue.cs
 * Purpose       : Fetches the published tool list from the tool source, checks every tool's
 *                 signature, and keeps the ones that pass. Everything the connector is willing to
 *                 run comes from here - nothing is compiled into the add-in.
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
 * Dependencies  : Newtonsoft.Json, System.Net.Http, ToolSignature
 *
 * Input         : A tool source - a local folder while developing, an https website later.
 * Output        : The verified tool list, plus counts of what was refused and why.
 *
 * Notes         :
 * - The source is a setting, not a constant, precisely so the same build works against a folder on
 *   Ajmal's machine today and against his website tomorrow with no code change. Setting lives at
 *   %APPDATA%\AJTools\connector.json; the default is a local folder.
 * - A ".ajtool" file is { "payload": <base64 of the tool JSON>, "signature": <base64 RSA-SHA256> }.
 *   The signature covers the payload BYTES EXACTLY AS SHIPPED. Signing the raw bytes rather than a
 *   re-serialised object is deliberate: any "canonical JSON" scheme has to make the signer and the
 *   verifier agree on key order, spacing and escaping, and every disagreement is either a false
 *   rejection or, worse, a way to alter a tool without breaking its signature.
 * - REFUSAL IS SILENT AND TOTAL. A tool that fails verification is not shown, not run, and not
 *   explained beyond a count. It is never "shown with a warning" - a button a person can press is a
 *   button someone will press.
 * - Verified tools are cached to disk so the panel still works with no internet, and so a website
 *   that goes down cannot silently empty a colleague's toolset. The cache holds the ORIGINAL signed
 *   files, and everything served from it is verified again on load - a cache is just a slower
 *   network, never a source of trust.
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AJToolsConnector
{
    /// <summary>A tool that passed signature verification and may be run.</summary>
    public class ConnectorTool
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Panel { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
    }

    /// <summary>On-disk shape of a published ".ajtool" file.</summary>
    internal class SignedToolFile
    {
        [JsonProperty("payload")] public string Payload { get; set; }
        [JsonProperty("signature")] public string Signature { get; set; }
    }

    public class CatalogueRefreshResult
    {
        public bool Success { get; set; }
        public int Accepted { get; set; }
        public int Refused { get; set; }
        public string Message { get; set; }
        public bool FromCache { get; set; }
    }

    public class ToolCatalogue
    {
        private static readonly HttpClient Http = new HttpClient();

        private readonly object _lock = new object();
        private List<ConnectorTool> _tools = new List<ConnectorTool>();

        public string Source { get; private set; }

        public IReadOnlyList<ConnectorTool> Tools
        {
            get { lock (_lock) { return _tools.ToArray(); } }
        }

        public ToolCatalogue()
        {
            Source = ReadConfiguredSource();
        }

        #region Locations

        private static string AppDataRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AJTools");
            }
        }

        private static string ConfigPath { get { return Path.Combine(AppDataRoot, "connector.json"); } }
        private static string DefaultToolFolder { get { return Path.Combine(AppDataRoot, "tools"); } }
        private static string CacheFolder { get { return Path.Combine(AppDataRoot, "connector-cache"); } }

        /// <summary>
        /// Where tools are published. A local folder while developing, an https URL once the website
        /// exists. Falls back to a local folder so a fresh install is never in a broken state.
        /// </summary>
        private static string ReadConfiguredSource()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var config = JsonConvert.DeserializeObject<ConnectorConfig>(File.ReadAllText(ConfigPath));
                    if (config != null && !string.IsNullOrWhiteSpace(config.Source))
                        return config.Source.Trim();
                }
            }
            catch { /* an unreadable setting must not stop the connector starting */ }

            return DefaultToolFolder;
        }

        private class ConnectorConfig
        {
            [JsonProperty("source")] public string Source { get; set; }
        }

        /// <summary>The folder a fresh install reads from, before any website is configured.</summary>
        public static string DefaultLocalFolder { get { return DefaultToolFolder; } }

        /// <summary>Current setting, for the AJ Connect window to show.</summary>
        public static string ReadSourceSetting() { return ReadConfiguredSource(); }

        /// <summary>
        /// Saves where tools come from. Takes effect on the next refresh - no restart, and no need to
        /// ship a colleague a new build just to move the website.
        /// </summary>
        public static void WriteSourceSetting(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("No source given.");

            string value = source.Trim();
            if (IsWebSource(value) && !value.EndsWith("/", StringComparison.Ordinal)) value += "/";

            string dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(new ConnectorConfig { Source = value }));
        }

        private static bool IsWebSource(string source)
        {
            return source != null
                && (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                 || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Refresh

        /// <summary>
        /// Re-reads the source, verifies everything, and replaces the live tool list. Falls back to
        /// the cache when a web source cannot be reached.
        /// </summary>
        public async Task<CatalogueRefreshResult> RefreshAsync()
        {
            Source = ReadConfiguredSource();

            List<SignedToolFile> files;
            bool fromCache = false;

            try
            {
                files = IsWebSource(Source)
                    ? await ReadFromWebAsync(Source).ConfigureAwait(false)
                    : ReadFromFolder(Source);

                if (IsWebSource(Source)) WriteCache(files);
            }
            catch (Exception ex)
            {
                files = ReadCache();
                fromCache = true;

                if (files.Count == 0)
                {
                    return new CatalogueRefreshResult
                    {
                        Success = false,
                        Message = "Could not reach the tool source and nothing is cached. " + ex.Message
                    };
                }
            }

            int refused;
            var verified = VerifyAll(files, out refused);

            lock (_lock) { _tools = verified; }

            return new CatalogueRefreshResult
            {
                Success = true,
                Accepted = verified.Count,
                Refused = refused,
                FromCache = fromCache,
                Message = BuildMessage(verified.Count, refused, fromCache)
            };
        }

        private static string BuildMessage(int accepted, int refused, bool fromCache)
        {
            string text = accepted + " tool(s) loaded";
            if (fromCache) text += " from the offline copy";
            if (refused > 0)
                text += ". " + refused + " REFUSED - not signed by Ajmal, or altered since publishing";
            return text + ".";
        }

        private static List<SignedToolFile> ReadFromFolder(string folder)
        {
            var results = new List<SignedToolFile>();
            if (!Directory.Exists(folder)) return results;

            foreach (string path in Directory.GetFiles(folder, "*.ajtool"))
            {
                try { results.Add(JsonConvert.DeserializeObject<SignedToolFile>(File.ReadAllText(path))); }
                catch { /* an unreadable file is simply not a tool */ }
            }

            return results;
        }

        /// <summary>
        /// Reads index.json (a plain list of file names) from the website, then each named file.
        /// The index is NOT trusted - it only says what to look at. Trust comes from each file's
        /// own signature, so a tampered index can add nothing runnable.
        /// </summary>
        private static async Task<List<SignedToolFile>> ReadFromWebAsync(string baseUrl)
        {
            string root = baseUrl.TrimEnd('/') + "/";
            string indexJson = await Http.GetStringAsync(root + "index.json").ConfigureAwait(false);
            var names = JsonConvert.DeserializeObject<List<string>>(indexJson) ?? new List<string>();

            var results = new List<SignedToolFile>();
            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                // Only a bare file name is ever appended - never a path the index chose, which would
                // otherwise let it walk to another host or a parent directory.
                string safeName = Path.GetFileName(name.Trim());
                if (string.IsNullOrEmpty(safeName)) continue;

                try
                {
                    string body = await Http.GetStringAsync(root + safeName).ConfigureAwait(false);
                    results.Add(JsonConvert.DeserializeObject<SignedToolFile>(body));
                }
                catch { /* one unreachable tool must not lose the rest */ }
            }

            return results;
        }

        private static List<ConnectorTool> VerifyAll(List<SignedToolFile> files, out int refused)
        {
            var accepted = new List<ConnectorTool>();
            refused = 0;

            foreach (var file in files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.Payload)) { refused++; continue; }

                byte[] payloadBytes;
                try { payloadBytes = Convert.FromBase64String(file.Payload); }
                catch { refused++; continue; }

                if (!ToolSignature.Verify(payloadBytes, file.Signature)) { refused++; continue; }

                ConnectorTool tool;
                try { tool = JsonConvert.DeserializeObject<ConnectorTool>(Encoding.UTF8.GetString(payloadBytes)); }
                catch { refused++; continue; }

                if (tool == null || string.IsNullOrWhiteSpace(tool.Id) || string.IsNullOrWhiteSpace(tool.Code))
                {
                    refused++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(tool.Name)) tool.Name = tool.Id;
                if (string.IsNullOrWhiteSpace(tool.Panel)) tool.Panel = "Tools";

                accepted.Add(tool);
            }

            return accepted;
        }

        public ConnectorTool Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            foreach (var tool in Tools)
            {
                if (string.Equals(tool.Id, id, StringComparison.OrdinalIgnoreCase)) return tool;
            }

            return null;
        }

        #endregion

        #region Offline cache

        private static void WriteCache(List<SignedToolFile> files)
        {
            try
            {
                if (!Directory.Exists(CacheFolder)) Directory.CreateDirectory(CacheFolder);

                foreach (string stale in Directory.GetFiles(CacheFolder, "*.ajtool"))
                {
                    try { File.Delete(stale); } catch { }
                }

                int index = 0;
                foreach (var file in files)
                {
                    if (file == null) continue;
                    File.WriteAllText(
                        Path.Combine(CacheFolder, "cached-" + index++ + ".ajtool"),
                        JsonConvert.SerializeObject(file));
                }
            }
            catch { /* the cache is a convenience - failing to write it must not fail a refresh */ }
        }

        private static List<SignedToolFile> ReadCache()
        {
            try { return ReadFromFolder(CacheFolder); }
            catch { return new List<SignedToolFile>(); }
        }

        #endregion
    }
}
