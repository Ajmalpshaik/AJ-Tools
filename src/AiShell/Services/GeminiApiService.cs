using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AJTools.AiShell.Models;
using AJTools.AiShell.Configuration;

namespace AJTools.AiShell.Services
{
    public class GeminiApiService : IAiProviderService
    {
        private readonly AiShellConfig _config;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public string ProviderName => "Gemini";

        public GeminiApiService(AiShellConfig config)
        {
            _config = config;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_config.GetGeminiApiKey());
        }

        // Keyed by API key (not a single field) so switching keys in Settings - without
        // restarting Revit - re-discovers the best model for the new key instead of reusing
        // whatever the previous key resolved to. This service instance lives for the whole
        // Revit session (constructed once in AiShellPaneProvider), so a single field would
        // otherwise never expire.
        private readonly Dictionary<string, string> _cachedModelByApiKey = new Dictionary<string, string>();

        private async Task<string> GetBestGeminiModelAsync(string apiKey, CancellationToken cancellationToken)
        {
            if (_cachedModelByApiKey.TryGetValue(apiKey, out var cachedModel)) return cachedModel;

            const string url = "https://generativelanguage.googleapis.com/v1beta/models";
            try
            {
                string json;
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Add("x-goog-api-key", apiKey);
                    var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return "gemini-1.5-flash"; // fallback
                    }

                    json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                dynamic result = JsonConvert.DeserializeObject(json);
                
                string selectedModel = null;
                var availableModels = new List<string>();

                if (result?.models != null)
                {
                    foreach (var model in result.models)
                    {
                        string name = model.name?.ToString() ?? ""; 
                        string methods = model.supportedGenerationMethods?.ToString() ?? "";
                        
                        if (methods.Contains("generateContent") && name.Contains("gemini"))
                        {
                            availableModels.Add(name.Replace("models/", ""));
                        }
                    }
                }

                if (availableModels.Count > 0)
                {
                    if (availableModels.Contains("gemini-1.5-flash")) selectedModel = "gemini-1.5-flash";
                    else if (availableModels.Contains("gemini-1.5-pro")) selectedModel = "gemini-1.5-pro";
                    else if (availableModels.Contains("gemini-pro")) selectedModel = "gemini-pro";
                    else if (availableModels.Contains("gemini-1.0-pro")) selectedModel = "gemini-1.0-pro";
                    else selectedModel = availableModels.First(); 
                }

                if (selectedModel == null)
                {
                     throw new Exception("No Gemini models supporting generateContent were found for your API key.");
                }

                _cachedModelByApiKey[apiKey] = selectedModel;
                return selectedModel;
            }
            catch (OperationCanceledException)
            {
                // Let Stop actually stop instead of silently continuing with a fallback model -
                // this must be checked before the generic catch below, which would otherwise
                // swallow the cancellation just like any other error.
                throw;
            }
            catch (Exception)
            {
                return "gemini-1.5-flash"; // fallback on any error
            }
        }

        public async Task<string> SendMessageAsync(List<ChatMessage> history, string systemPrompt = null, CancellationToken cancellationToken = default)
        {
            var apiKey = _config.GetGeminiApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Gemini API Key is not configured.");
            }

            // Dynamically select the best available model for this API key
            string modelName = await GetBestGeminiModelAsync(apiKey, cancellationToken).ConfigureAwait(false);
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";

            var requestBody = new GeminiRequest();
            
            // Build contents list without mutating the caller's history
            foreach (var msg in history)
            {
                string content = msg.Content;
                
                // Inject system prompt into the last user message (as a clone)
                if (!string.IsNullOrWhiteSpace(systemPrompt) && msg.Role == "user" && msg == history.LastOrDefault(m => m.Role == "user"))
                {
                    content = systemPrompt + "\n\n" + content;
                }

                requestBody.contents.Add(new Content
                {
                    role = msg.Role,
                    parts = new List<Part> { new Part { text = content } }
                });
            }

            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            string responseJson;
            using (var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = jsonContent })
            {
                request.Headers.Add("x-goog-api-key", apiKey);
                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"Gemini API Error ({response.StatusCode}): {err}");
                }

                responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            var geminiResponse = JsonConvert.DeserializeObject<GeminiResponse>(responseJson);

            var reply = geminiResponse?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text;
            return reply ?? string.Empty;
        }
    }
}
