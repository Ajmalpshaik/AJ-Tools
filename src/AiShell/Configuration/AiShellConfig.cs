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

        public string SelectedProvider { get; set; } = "Gemini"; // "Gemini", "OpenAI", or "Claude"

        public string EncryptedGeminiApiKey { get; set; }
        public string EncryptedOpenAiApiKey { get; set; }
        public string EncryptedAnthropicApiKey { get; set; }

        public string OpenAiModel { get; set; } = "gpt-4o";
        public string AnthropicModel { get; set; } = "claude-sonnet-5";

        public string ScriptsFolderPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AJTools_Scripts");

        /// <summary>Full path of the saved .cs script currently pinned to the "Run Pinned Script"
        /// ribbon button (RunPinnedScriptCommand). Null/empty means nothing is pinned.</summary>
        public string PinnedScriptPath { get; set; }

        public static AiShellConfig Load()
        {
            lock (_fileLock)
            {
                if (File.Exists(ConfigPath))
                {
                    try
                    {
                        var json = File.ReadAllText(ConfigPath);
                        return JsonConvert.DeserializeObject<AiShellConfig>(json) ?? new AiShellConfig();
                    }
                    catch (Exception ex)
                    {
                        LogError("Failed to load config", ex);
                        return new AiShellConfig();
                    }
                }
                return new AiShellConfig();
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

        public string GetGeminiApiKey() => Unprotect(EncryptedGeminiApiKey);
        public string GetOpenAiApiKey() => Unprotect(EncryptedOpenAiApiKey);
        public string GetAnthropicApiKey() => Unprotect(EncryptedAnthropicApiKey);

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
