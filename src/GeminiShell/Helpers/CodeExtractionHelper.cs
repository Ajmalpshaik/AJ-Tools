using System;
using System.Text.RegularExpressions;

namespace AJTools.GeminiShell.Helpers
{
    public static class CodeExtractionHelper
    {
        public static string ExtractCSharpCode(string aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse))
                return string.Empty;

            // Pattern to match C# code blocks: ```csharp ... ``` or ```cs ... ``` or just ``` ... ```
            var matches = Regex.Matches(aiResponse, @"```(?:csharp|cs)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
            
            if (matches.Count > 0)
            {
                // Return the LAST code block (AI often puts the corrected/final code last)
                return matches[matches.Count - 1].Groups[1].Value.Trim();
            }

            // If no markdown fence is found, return the original assuming the AI returned pure code.
            return aiResponse.Trim();
        }
    }
}
