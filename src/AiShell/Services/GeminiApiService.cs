using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AJTools.AiShell.Models;
using AJTools.AiShell.Configuration;

namespace AJTools.AiShell.Services
{
    public class GeminiApiService : IAiProviderService
    {
        private readonly AiShellConfig _config;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        // A stable general model id as the model list names it: version, family, and at most a
        // three-digit pinned revision - "gemini-2.5-flash", "gemini-2.0-flash-001", "gemini-2.5-pro".
        // Anything else in the id (preview, exp, lite, tts, image, latest, ...) is not a stable
        // general-purpose model and is not preferred.
        private static readonly Regex StableModelIdRegex = new Regex(
            @"^gemini-(\d+(?:\.\d+)?)-(flash|pro)(?:-(\d{3}))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

        /// <summary>Picks the model from the list Gemini itself returns for this key - never a
        /// hard-coded id, which goes stale as models are retired. Preference: the newest stable
        /// "flash" model that can generateContent, then the newest stable "pro", then the first
        /// listed generateContent model. A failed listing is reported to the user, not papered over
        /// with a guessed id.</summary>
        private async Task<string> GetBestGeminiModelAsync(string apiKey, CancellationToken cancellationToken)
        {
            if (_cachedModelByApiKey.TryGetValue(apiKey, out var cachedModel)) return cachedModel;

            // pageSize=1000 is the documented per-page maximum, so the whole list comes back in one
            // page (the default page of 50 can leave the newest models on a later page).
            const string url = "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000";

            string json;
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Add("x-goog-api-key", apiKey);

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // HttpClient reports its own timeout as TaskCanceledException - without this
                    // check it read as if the user had pressed Stop.
                    throw new Exception(TimeoutMessage());
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new Exception($"Could not get the list of Gemini models for this API key ({response.StatusCode}): {err}");
                    }

                    json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }

            var availableModels = new List<string>();
            var models = JObject.Parse(json)["models"] as JArray;
            if (models != null)
            {
                foreach (var model in models)
                {
                    string name = model["name"]?.ToString() ?? string.Empty;
                    var methods = model["supportedGenerationMethods"] as JArray;
                    bool canGenerate = methods != null &&
                                       methods.Any(m => string.Equals(m?.ToString(), "generateContent", StringComparison.Ordinal));

                    if (canGenerate && name.Contains("gemini"))
                    {
                        availableModels.Add(name.Replace("models/", ""));
                    }
                }
            }

            string selectedModel = PickNewestStable(availableModels, "flash")
                                   ?? PickNewestStable(availableModels, "pro")
                                   ?? availableModels.FirstOrDefault();

            if (selectedModel == null)
            {
                throw new Exception("No Gemini models supporting generateContent were found for your API key.");
            }

            _cachedModelByApiKey[apiKey] = selectedModel;
            return selectedModel;
        }

        /// <summary>The newest stable model of one family ("flash" / "pro") in the list, or null. A
        /// plain id ("gemini-2.5-flash") always follows the newest stable revision, so it beats a
        /// pinned revision ("gemini-2.5-flash-001") of the same version.</summary>
        private static string PickNewestStable(List<string> modelIds, string family)
        {
            string bestId = null;
            Version bestVersion = null;
            int bestRevision = -1;

            foreach (string id in modelIds)
            {
                Match match = StableModelIdRegex.Match(id);
                if (!match.Success || !string.Equals(match.Groups[2].Value, family, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string versionText = match.Groups[1].Value;
                if (!versionText.Contains(".")) versionText += ".0";
                if (!Version.TryParse(versionText, out Version version))
                {
                    continue;
                }

                int revision = match.Groups[3].Success
                    ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
                    : int.MaxValue;

                if (bestVersion == null || version > bestVersion || (version == bestVersion && revision > bestRevision))
                {
                    bestId = id;
                    bestVersion = version;
                    bestRevision = revision;
                }
            }

            return bestId;
        }

        private string TimeoutMessage() =>
            $"The AI service did not answer in time ({ProviderName}, {_httpClient.Timeout.TotalSeconds:0} seconds). " +
            "Try again, or ask for something shorter.";

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

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // HttpClient's own timeout, not the user's Stop - see GetBestGeminiModelAsync.
                    throw new Exception(TimeoutMessage());
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new Exception($"Gemini API Error ({response.StatusCode}): {err}");
                    }

                    responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            var geminiResponse = JsonConvert.DeserializeObject<GeminiResponse>(responseJson);

            var firstCandidate = geminiResponse?.candidates?.FirstOrDefault();
            var reply = firstCandidate?.content?.parts?.FirstOrDefault()?.text;
            if (string.IsNullOrWhiteSpace(reply))
            {
                // Say WHY when Gemini says why - a blocked request or a stopped answer comes back as
                // "no text", which used to be handed on as an empty reply.
                string blockReason = geminiResponse?.promptFeedback?.blockReason;
                string finishReason = firstCandidate?.finishReason;
                if (!string.IsNullOrWhiteSpace(blockReason))
                {
                    throw new AiEmptyReplyException($"Gemini blocked the request ({blockReason}).");
                }
                if (!string.IsNullOrWhiteSpace(finishReason) && !string.Equals(finishReason, "STOP", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AiEmptyReplyException($"Gemini stopped without writing anything (finish reason: {finishReason}).");
                }
                throw new AiEmptyReplyException("Gemini sent back an empty reply.");
            }

            return reply;
        }
    }
}
