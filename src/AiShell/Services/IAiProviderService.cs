using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AJTools.AiShell.Models;

namespace AJTools.AiShell.Services
{
    public interface IAiProviderService
    {
        string ProviderName { get; }
        Task<string> SendMessageAsync(List<ChatMessage> history, string systemPrompt = null, CancellationToken cancellationToken = default);
        bool IsConfigured();
    }
}
