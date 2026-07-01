#region Metadata
/*
 * Tool Name     : AJ AI (Gemini Shell)
 * File Name     : ErrorCorrectionService.cs
 * Purpose       : When an AI-generated script fails, sends the exact error plus the current code
 *                 back to the AI and asks for a MINIMAL, intent-preserving fix — not a fresh
 *                 rewrite — so working logic is kept and only the failing part is corrected.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-01-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : IAiProviderService, CodeExtractionHelper
 *
 * Input         : The failed code, the exact error text (from the output console), chat history,
 *                 and the shared system prompt
 * Output        : Corrected C# code (fixed, not regenerated from scratch)
 *
 * Notes         :
 * - The fix prompt explicitly instructs the AI to change only what caused the error and keep the
 *   rest of the code and the user's original goal intact, to avoid the model drifting into a
 *   completely different solution on each retry.
 *
 * Changelog     :
 * v1.0.0 (2026-01-01) - Initial release.
 * v1.1.0 (2026-07-01) - Reworked the fix prompt to force minimal, intent-preserving corrections
 *                       driven by the actual error; added mandatory metadata block.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AJTools.GeminiShell.Models;
using AJTools.GeminiShell.Helpers;

namespace AJTools.GeminiShell.Services
{
    public class ErrorCorrectionService
    {
        private readonly IAiProviderService _aiService;

        public ErrorCorrectionService(IAiProviderService aiService)
        {
            _aiService = aiService;
        }

        public async Task<string> RequestFixAsync(string originalCode, string errorMessage, List<GeminiMessage> history, string systemPrompt, CancellationToken cancellationToken = default)
        {
            var prompt = $@"The code you provided was run in Revit and FAILED with this exact error:

--- ERROR ---
{errorMessage}
--- END ERROR ---

Here is the current code that produced that error:

--- CODE ---
{originalCode}
--- END CODE ---

Fix it using these rules:
1. Change ONLY what is needed to remove this specific error. Keep the same overall approach and keep the parts that already work.
2. Do NOT rewrite the script from scratch and do NOT switch to a completely different method — the user's original goal must stay the same.
3. Read the error carefully and correct the real cause (a wrong API member, a null value, a missing check, a wrong parameter name, a 2021+ API used on Revit 2020, etc.).
4. Stay within the Revit 2020 API and all the safety rules already given.
5. Output ONLY the complete corrected C# code, nothing else.";

            // Append the fix request to a copy of the history so the AI keeps the original intent.
            var tempHistory = new List<GeminiMessage>(history)
            {
                new GeminiMessage { Role = "user", Content = prompt }
            };

            var aiResponse = await _aiService.SendMessageAsync(tempHistory, systemPrompt, cancellationToken);
            return CodeExtractionHelper.ExtractCSharpCode(aiResponse);
        }
    }
}
