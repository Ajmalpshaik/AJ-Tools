using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AJTools.GeminiShell.Models;
using AJTools.GeminiShell.Configuration;

namespace AJTools.GeminiShell.Services
{
    public class OpenAiApiService : IAiProviderService
    {
        private readonly GeminiShellConfig _config;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public string ProviderName => "OpenAI";

        public OpenAiApiService(GeminiShellConfig config)
        {
            _config = config;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_config.GetOpenAiApiKey());
        }

        public async Task<string> SendMessageAsync(List<GeminiMessage> history, string systemPrompt = null, CancellationToken cancellationToken = default)
        {
            var apiKey = _config.GetOpenAiApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenAI API Key is not configured.");
            }

            string url = "https://api.openai.com/v1/chat/completions";

            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            foreach (var msg in history)
            {
                // Map "model" to "assistant" for OpenAI
                var role = msg.Role == "model" ? "assistant" : "user";
                messages.Add(new { role = role, content = msg.Content });
            }

            string modelName = string.IsNullOrWhiteSpace(_config.OpenAiModel) ? "gpt-4o" : _config.OpenAiModel.Trim();
            
            var requestBody = new
            {
                model = modelName,
                messages = messages,
                temperature = 0.2
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            // Use per-request headers instead of DefaultRequestHeaders (thread-safe)
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = jsonContent;

                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"OpenAI API Error ({response.StatusCode}): {err}");
                }

                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                
                // Dynamic parsing with null safety
                dynamic result = JsonConvert.DeserializeObject(responseJson);
                
                if (result?.choices != null && result.choices.Count > 0)
                {
                    string reply = result.choices[0]?.message?.content?.ToString();
                    return reply ?? string.Empty;
                }

                return string.Empty;
            }
        }
    }
}
