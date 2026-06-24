using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AJTools.GeminiShell.Models;

namespace AJTools.GeminiShell.Services
{
    public interface IAiProviderService
    {
        string ProviderName { get; }
        Task<string> SendMessageAsync(List<GeminiMessage> history, string systemPrompt = null, CancellationToken cancellationToken = default);
        bool IsConfigured();
    }
}
