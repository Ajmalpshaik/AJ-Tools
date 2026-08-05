#region Metadata
/*
 * Tool Name     : C#
 * File Name     : NvidiaApiService.cs
 * Purpose       : Fourth IAiProviderService implementation for the "C#" AI shell - talks to NVIDIA's
 *                 hosted NIM catalog (build.nvidia.com) so the pane can drive ~130 open models
 *                 (GLM, Qwen Coder, DeepSeek, Nemotron) on NVIDIA's free tier, alongside the paid
 *                 Gemini/OpenAI/Claude providers.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-05
 * Last Updated  : 2026-08-05
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (no Revit API access - pure HTTPS + config)
 *
 * Dependencies  : AiShellConfig (key + model), ChatMessage, Newtonsoft.Json
 *
 * Input         : Chat history + system prompt from AiShellViewModel.
 * Output        : The model's reply text, with any reasoning-trace markup stripped.
 *
 * Notes         :
 * - NIM is OpenAI-wire-compatible, so this is deliberately the OpenAiApiService shape: same
 *   /chat/completions body, same "Bearer" header, same choices[0].message.content parse. Only the
 *   base address, the key and the four settings below differ. Keys are nvapi-... from build.nvidia.com.
 * - FOUR DELIBERATE DIFFERENCES FROM OpenAiApiService, all forced by the default model being a
 *   REASONING model (z-ai/glm-5.2 - 753B, thinks at length before answering):
 *   (1) 180s timeout, not the 60s the other three share. A reasoning model working through a Revit
 *       scripting problem can genuinely run past a minute; on the shared client that surfaced as a
 *       timeout exception, which reads like a bad key rather than "still thinking". Its own static
 *       HttpClient so raising it here cannot slow down or destabilise the other providers.
 *   (2) max_tokens 16384 (NVIDIA's own published sample value for glm-5.2). Reasoning tokens are
 *       billed against the SAME budget as the answer, so a low cap truncates the generated script
 *       mid-function after the model has spent the allowance thinking. Claude's 8192 is safe only
 *       because Claude has no hidden reasoning spend here.
 *   (3) temperature/top_p = 1, NOT the 0.2 OpenAiApiService uses. Clamping a reasoning model
 *       degrades its chain of thought; 1/1 is what NVIDIA ships in the glm-5.2 sample.
 *   (4) No "seed". NVIDIA's sample sets seed=42, which makes replies repeatable - and that is the
 *       wrong trade here: a repeatable seed means a retry after a broken script returns the
 *       IDENTICAL broken script. The auto-fix retry loop in AiShellViewModel depends on a retry
 *       being a genuinely fresh attempt.
 * - Reasoning arrives in a SEPARATE "reasoning_content" field on this API, so content is already
 *   clean for glm-5.2. StripReasoningTrace is insurance for the other NIM models that inline it as
 *   <think>...</think> - that markup reaching CodeExtractionHelper would confuse code extraction.
 * - Streaming is deliberately NOT used. NVIDIA's sample sets stream=True, but that only changes how
 *   the reply is delivered, not what it contains, and IAiProviderService is a single-reply contract
 *   shared by all four providers. Adding real streaming means changing that shared interface and is
 *   its own piece of work (Ajmal deferred it 2026-08-05).
 *
 * Changelog     :
 * v1.0.0 (2026-08-05) - Initial release. NVIDIA NIM as the fourth provider, default model
 *                       z-ai/glm-5.2, per Ajmal's request to use the build.nvidia.com free catalog.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AJTools.AiShell.Models;
using AJTools.AiShell.Configuration;

namespace AJTools.AiShell.Services
{
    public class NvidiaApiService : IAiProviderService
    {
        private readonly AiShellConfig _config;

        // Own client (not the 60s one the other providers share) - see note (1) in the header.
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };

        private const string Url = "https://integrate.api.nvidia.com/v1/chat/completions";
        private const string DefaultModel = "z-ai/glm-5.2";
        private const int MaxOutputTokens = 16384;

        /// <summary>Matches an inline reasoning block from NIM models that don't use the separate
        /// reasoning_content field. Singleline so "." spans the newlines inside the block.</summary>
        private static readonly Regex ThinkBlockRegex =
            new Regex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>An unterminated opening block - happens when the reply hit max_tokens mid-thought.
        /// Dropping everything from the tag onward is right: there is no answer after it.</summary>
        private static readonly Regex UnclosedThinkRegex =
            new Regex(@"<think>.*$", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        public string ProviderName => "NVIDIA";

        public NvidiaApiService(AiShellConfig config)
        {
            _config = config;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_config.GetNvidiaApiKey());
        }

        public async Task<string> SendMessageAsync(List<ChatMessage> history, string systemPrompt = null, CancellationToken cancellationToken = default)
        {
            var apiKey = _config.GetNvidiaApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("NVIDIA API Key is not configured.");
            }

            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            foreach (var msg in history)
            {
                // Same role mapping as OpenAI - this API only knows user/assistant/system.
                var role = msg.Role == "model" ? "assistant" : "user";
                messages.Add(new { role = role, content = msg.Content });
            }

            string modelName = string.IsNullOrWhiteSpace(_config.NvidiaModel) ? DefaultModel : _config.NvidiaModel.Trim();

            var requestBody = new
            {
                model = modelName,
                messages = messages,
                temperature = 1.0,
                top_p = 1.0,
                max_tokens = MaxOutputTokens
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            // Per-request headers rather than DefaultRequestHeaders, so the shared static client
            // stays thread-safe (same reason as OpenAiApiService).
            using (var request = new HttpRequestMessage(HttpMethod.Post, Url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = jsonContent;

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // HttpClient reports its own timeout as TaskCanceledException, which is
                    // indistinguishable from a user Stop unless the token is checked. Without this
                    // the message would read as if Ajmal had cancelled the request himself.
                    throw new Exception(
                        $"NVIDIA API timed out after 180 seconds ({modelName}). Reasoning models can be slow " +
                        "on long prompts - try a shorter request, or pick a faster model in Settings.");
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new Exception($"NVIDIA API Error ({response.StatusCode}): {err}");
                    }

                    var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    dynamic result = JsonConvert.DeserializeObject(responseJson);

                    if (result?.choices == null || result.choices.Count == 0)
                    {
                        return string.Empty;
                    }

                    var firstChoice = result.choices[0];
                    string reply = firstChoice?.message?.content?.ToString() ?? string.Empty;
                    string finishReason = firstChoice?.finish_reason?.ToString();

                    reply = StripReasoningTrace(reply);

                    // "length" means the token budget ran out. With a reasoning model that usually
                    // means thinking consumed the allowance, leaving no answer (or half a script) -
                    // silently returning that would surface later as a confusing compile error.
                    if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(reply))
                    {
                        throw new Exception(
                            $"NVIDIA model {modelName} used its entire {MaxOutputTokens}-token budget on reasoning " +
                            "and returned no answer. Try a shorter or simpler request.");
                    }

                    return reply;
                }
            }
        }

        /// <summary>Removes inline &lt;think&gt; reasoning markup. glm-5.2 returns reasoning in a
        /// separate field so this is normally a no-op; other NIM models inline it, and that text
        /// reaching the code extractor would be read as part of the script.</summary>
        private static string StripReasoningTrace(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return reply ?? string.Empty;

            var cleaned = ThinkBlockRegex.Replace(reply, string.Empty);
            cleaned = UnclosedThinkRegex.Replace(cleaned, string.Empty);
            return cleaned.Trim();
        }
    }
}
