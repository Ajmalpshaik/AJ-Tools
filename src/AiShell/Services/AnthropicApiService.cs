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
    public class AnthropicApiService : IAiProviderService
    {
        private readonly AiShellConfig _config;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        // Anthropic requires max_tokens on every request (no server-side default, unlike
        // Gemini/OpenAI) - 8192 comfortably covers a generated Revit script/summary without
        // risking a response longer than the shared 60s HttpClient timeout can finish.
        private const int MaxOutputTokens = 8192;
        private const string ApiVersion = "2023-06-01";

        public string ProviderName => "Claude";

        public AnthropicApiService(AiShellConfig config)
        {
            _config = config;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_config.GetAnthropicApiKey());
        }

        public async Task<string> SendMessageAsync(List<ChatMessage> history, string systemPrompt = null, CancellationToken cancellationToken = default)
        {
            var apiKey = _config.GetAnthropicApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Claude API Key is not configured.");
            }

            const string url = "https://api.anthropic.com/v1/messages";

            // Claude only accepts "user"/"assistant" roles in the messages array - the system
            // prompt is a separate top-level field, not a message (unlike OpenAI's "system" role).
            var messages = new List<object>();
            foreach (var msg in history)
            {
                var role = msg.Role == "model" ? "assistant" : "user";
                messages.Add(new { role, content = msg.Content });
            }

            string modelName = string.IsNullOrWhiteSpace(_config.AnthropicModel) ? "claude-sonnet-5" : _config.AnthropicModel.Trim();

            var requestBody = new Dictionary<string, object>
            {
                ["model"] = modelName,
                ["max_tokens"] = MaxOutputTokens,
                ["messages"] = messages
            };
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                requestBody["system"] = systemPrompt;
            }

            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            using (var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = jsonContent })
            {
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", ApiVersion);

                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"Claude API Error ({response.StatusCode}): {err}");
                }

                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                dynamic result = JsonConvert.DeserializeObject(responseJson);

                // content is an array of blocks (text, possibly others) - concatenate every
                // text block in order rather than assuming there is exactly one.
                if (result?.content == null) return string.Empty;

                var sb = new StringBuilder();
                foreach (var block in result.content)
                {
                    string blockType = block?.type?.ToString();
                    if (blockType == "text")
                    {
                        sb.Append(block.text?.ToString() ?? string.Empty);
                    }
                }
                return sb.ToString();
            }
        }
    }
}
