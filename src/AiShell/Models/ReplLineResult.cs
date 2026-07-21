using Microsoft.CodeAnalysis.Scripting;

namespace AJTools.AiShell.Models
{
    /// <summary>Result of one interactive-console line (RoslynService.ExecuteReplLineAsync). Kept
    /// separate from CodeExecutionResult because a REPL line also needs to hand back the resulting
    /// ScriptState so the caller can carry it into the next line.</summary>
    public class ReplLineResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string ErrorMessage { get; set; }
        public ScriptState<object> NewState { get; set; }
    }
}
