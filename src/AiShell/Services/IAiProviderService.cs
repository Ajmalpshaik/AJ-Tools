using System;
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

    /// <summary>
    /// Thrown by a provider when its reply carried no text at all (a safety block, a refusal, or
    /// simply nothing), with the provider's own stated reason in the message when it gave one. An
    /// empty string handed back as "the reply" used to wipe the Code Editor and still read as a
    /// success, so an empty reply is now always a plain, explained failure instead.
    /// </summary>
    public class AiEmptyReplyException : Exception
    {
        public AiEmptyReplyException(string message) : base(message)
        {
        }
    }
}
