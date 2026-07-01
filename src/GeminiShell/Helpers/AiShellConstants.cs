#region Metadata
/*
 * Tool Name     : AJ AI (Gemini Shell)
 * File Name     : AiShellConstants.cs
 * Purpose       : Shared constants for the AJ AI tool (retry limits, history size, context payload
 *                 cap) so these values are defined once instead of scattered as magic numbers.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : N/A (constants)
 * Output        : N/A (constants)
 *
 * Notes         :
 * - Added as part of the AJ AI safety/architecture hardening pass.
 *
 * Changelog     :
 * v1.0.0 (2026-07-01) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.GeminiShell.Helpers
{
    public static class AiShellConstants
    {
        /// <summary>Maximum number of automatic AI fix-and-retry attempts after a failed run.</summary>
        public const int MaxAutoFixAttempts = 5;

        /// <summary>Maximum number of chat messages kept in the in-memory conversation history.</summary>
        public const int MaxChatHistoryMessages = 50;

        /// <summary>Maximum characters of Revit context sent to the AI provider per request.</summary>
        public const int MaxContextPayloadChars = 4000;

        /// <summary>Maximum number of selected elements described in detail in the context summary.</summary>
        public const int MaxContextElementDetails = 25;
    }
}
