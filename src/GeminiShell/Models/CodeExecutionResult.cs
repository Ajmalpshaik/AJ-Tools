using System;

namespace AJTools.GeminiShell.Models
{
    public class CodeExecutionResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
