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
            var prompt = $@"
The previous code failed with the following error:

{errorMessage}

Here is the original code that caused the error:

{originalCode}

Please analyze the error and provide the corrected C# code. Only output the corrected code.
Target API: Revit 2020.
";

            // We append this prompt to a temporary history to get the correction
            var tempHistory = new List<GeminiMessage>(history)
            {
                new GeminiMessage { Role = "user", Content = prompt }
            };

            var aiResponse = await _aiService.SendMessageAsync(tempHistory, systemPrompt, cancellationToken);
            return CodeExtractionHelper.ExtractCSharpCode(aiResponse);
        }
    }
}
