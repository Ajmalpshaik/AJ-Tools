using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace AJTools.AiShell.Configuration
{
    public class AiShellConfig
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AJTools",
            "AiShellConfig.json"
        );

        private static readonly object _fileLock = new object();

        // The one copy of the settings this Revit session works with. The C# pane, the Saved Scripts
        // window and Run Pinned all use it, because Save() writes the WHOLE object: with separate
        // copies, a pin or folder set in Saved Scripts was written back to its old value the next
        // time the pane saved anything (provider, key, folder) from the copy it loaded at startup.
        private static AiShellConfig _shared;

        public string SelectedProvider { get; set; } = "Gemini"; // "Gemini", "OpenAI", "Claude", or "NVIDIA"

        public string EncryptedGeminiApiKey { get; set; }
        public string EncryptedOpenAiApiKey { get; set; }
        public string EncryptedAnthropicApiKey { get; set; }
        public string EncryptedNvidiaApiKey { get; set; }

        public string OpenAiModel { get; set; } = "gpt-4o";
        public string AnthropicModel { get; set; } = "claude-sonnet-5";

        /// <summary>NVIDIA NIM model id, exactly as shown on the build.nvidia.com model card
        /// (e.g. "z-ai/glm-5.2"). Free text rather than one fixed list - the catalog runs to ~130
        /// models, so Settings offers a fixed shortlist dropdown PLUS a plain text box (both bound to
        /// this value) where any other id can be pasted in.</summary>
        public string NvidiaModel { get; set; } = "z-ai/glm-5.2";

        public string ScriptsFolderPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AJTools_Scripts");

        /// <summary>Full path of the saved .cs script currently pinned to the "Run Pinned Script"
        /// ribbon button (RunPinnedScriptCommand). Null/empty means nothing is pinned.</summary>
        public string PinnedScriptPath { get; set; }

        /// <summary>Set when the settings file existed but could not be read, so the defaults are in
        /// use - a plain-language note for the C# pane to show once. Never written to the file.</summary>
        [JsonIgnore]
        public string LoadWarning { get; private set; }

        /// <summary>The settings instance shared by every part of the C# tools in this Revit session
        /// (loaded from disk on first use). Use this rather than Load() so two parts never hold
        /// different copies of the same file.</summary>
        public static AiShellConfig Shared
        {
            get
            {
                lock (_fileLock)
                {
                    return _shared ?? (_shared = Load());
                }
            }
        }

        public static AiShellConfig Load()
        {
            lock (_fileLock)
            {
                if (!File.Exists(ConfigPath))
                {
                    return new AiShellConfig();
                }

                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    var loaded = JsonConvert.DeserializeObject<AiShellConfig>(json);
                    if (loaded != null)
                    {
                        return loaded;
                    }

                    // An empty file (e.g. one cut short while being written) parses to null.
                    throw new InvalidDataException("The settings file is empty.");
                }
                catch (Exception ex)
                {
                    // Never fail silently here: the defaults have no API keys, pin or folder, and the
                    // next save would write them over the file. Keep a copy of the unreadable file
                    // first, and tell the user once (AiShellViewModel shows LoadWarning).
                    LogError("Failed to load config", ex);
                    return new AiShellConfig { LoadWarning = BackUpUnreadableFile() };
                }
            }
        }

        /// <summary>Copies the unreadable settings file aside (AiShellConfig.json.yyyyMMdd-HHmmss.bak,
        /// same folder) and returns the note the pane shows. Caller holds _fileLock.</summary>
        private static string BackUpUnreadableFile()
        {
            try
            {
                string backupPath = ConfigPath + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
                File.Copy(ConfigPath, backupPath, true);
                return "AI settings file could not be read - defaults in use. Old file kept as " +
                       Path.GetFileName(backupPath) + " (in %AppData%\\AJTools).";
            }
            catch (Exception ex)
            {
                LogError("Failed to back up the unreadable config", ex);
                return "AI settings file could not be read - defaults in use (the old file could not be copied aside).";
            }
        }

        public void Save()
        {
            lock (_fileLock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(ConfigPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    File.WriteAllText(ConfigPath, json);
                }
                catch (Exception ex)
                {
                    LogError("Failed to save config", ex);
                }
            }
        }

        public void SetGeminiApiKey(string key)
        {
            EncryptedGeminiApiKey = Protect(key);
            Save();
        }

        public void SetOpenAiApiKey(string key)
        {
            EncryptedOpenAiApiKey = Protect(key);
            Save();
        }

        public void SetAnthropicApiKey(string key)
        {
            EncryptedAnthropicApiKey = Protect(key);
            Save();
        }

        public void SetNvidiaApiKey(string key)
        {
            EncryptedNvidiaApiKey = Protect(key);
            Save();
        }

        public string GetGeminiApiKey() => Unprotect(EncryptedGeminiApiKey);
        public string GetOpenAiApiKey() => Unprotect(EncryptedOpenAiApiKey);
        public string GetAnthropicApiKey() => Unprotect(EncryptedAnthropicApiKey);
        public string GetNvidiaApiKey() => Unprotect(EncryptedNvidiaApiKey);

        private static string Protect(string clearText)
        {
            if (string.IsNullOrEmpty(clearText)) return null;
            var bytes = Encoding.UTF8.GetBytes(clearText);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private static string Unprotect(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return null;
            try
            {
                var bytes = Convert.FromBase64String(encryptedText);
                var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                LogError("Failed to decrypt API key (profile may have changed)", ex);
                return null;
            }
        }

        private static void LogError(string context, Exception ex)
        {
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AJTools");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "AiShell_Error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex.Message}\n");
            }
            catch { /* Last resort: can't even log */ }
        }
    }
}
