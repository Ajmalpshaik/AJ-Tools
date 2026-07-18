using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AJTools.AiShell.Models;
using AJTools.AiShell.Helpers;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.AiShell.Services
{
    public class RoslynService
    {
        // Cache compiled, already-rewritten scripts only. Never cache Revit elements or model-query
        // results: the document can change between calls and every script must see live model state.
        private const int MaxCompiledScriptCacheEntries = 64;

        private static readonly ScriptOptions SharedScriptOptions = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,                            // mscorlib
                typeof(Enumerable).Assembly,                        // System.Core
                typeof(System.Collections.Generic.List<>).Assembly,  // mscorlib
                typeof(System.IO.File).Assembly,                     // mscorlib (System.IO)
                typeof(System.Text.StringBuilder).Assembly,          // mscorlib (System.Text)
                typeof(System.Text.RegularExpressions.Regex).Assembly, // System
                typeof(System.Net.WebClient).Assembly,               // System (kept even though the safety
                                                                      // validator blocks WebClient by name -
                                                                      // on .NET 8 targets this may be a
                                                                      // different assembly than Regex's, and
                                                                      // removing it can't be verified safe
                                                                      // across every Revit-year build without
                                                                      // a compiler for each one)
                typeof(Document).Assembly,                          // RevitAPI
                typeof(UIDocument).Assembly                         // RevitAPIUI
            )
            .WithImports(
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.IO",
                "System.Text",
                "System.Math",
                "Autodesk.Revit.DB",
                "Autodesk.Revit.UI",
                "Autodesk.Revit.ApplicationServices"
            );

        private static readonly CSharpParseOptions ScriptParseOptions = new CSharpParseOptions(kind: SourceCodeKind.Script);

        private readonly ConcurrentDictionary<string, Script<object>> _compiledScriptCache =
            new ConcurrentDictionary<string, Script<object>>(StringComparer.Ordinal);

        private readonly ConcurrentQueue<string> _compiledScriptCacheOrder = new ConcurrentQueue<string>();

        public async Task<CodeExecutionResult> ExecuteAsync(string code, RevitScriptGlobals globals)
        {
            var result = new CodeExecutionResult();

            try
            {
                Script<object> script;
                if (!_compiledScriptCache.TryGetValue(code, out script))
                {
                    // Apply Infinite Loop Protection before the first compile. Subsequent identical
                    // requests reuse this safe compiled script, but each still receives fresh globals.
                    var syntaxTree = CSharpSyntaxTree.ParseText(code, ScriptParseOptions);
                    var rewriter = new LoopProtectionRewriter();
                    var safeRoot = rewriter.Visit(syntaxTree.GetRoot());
                    var safeCode = safeRoot.ToFullString();

                    var compiledScript = CSharpScript.Create(safeCode, SharedScriptOptions, typeof(RevitScriptGlobals));
                    var diagnostics = compiledScript.Compile();
                    if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
                    {
                        result.Success = false;
                        result.ErrorMessage = string.Join("\n", diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).Select(d => d.ToString()));
                        return result;
                    }

                    if (_compiledScriptCache.TryAdd(code, compiledScript))
                    {
                        _compiledScriptCacheOrder.Enqueue(code);
                        TrimCompiledScriptCache();
                        script = compiledScript;
                    }
                    else if (!_compiledScriptCache.TryGetValue(code, out script))
                    {
                        // The cache is an optimization only; execute the verified script even if a
                        // competing request evicted it before this call observed the cached entry.
                        script = compiledScript;
                    }
                }

                var scriptState = await script.RunAsync(globals).ConfigureAwait(false);
                
                result.Success = true;
                result.Output = scriptState.ReturnValue?.ToString() ?? "Execution completed successfully.";
            }
            catch (CompilationErrorException e)
            {
                result.Success = false;
                result.ErrorMessage = string.Join(Environment.NewLine, e.Diagnostics);
                result.Exception = e;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
                result.Exception = ex.InnerException ?? ex;
            }

            return result;
        }

        private void TrimCompiledScriptCache()
        {
            while (_compiledScriptCache.Count > MaxCompiledScriptCacheEntries)
            {
                string oldestCode;
                if (!_compiledScriptCacheOrder.TryDequeue(out oldestCode)) return;

                Script<object> discarded;
                _compiledScriptCache.TryRemove(oldestCode, out discarded);
            }
        }
    }
}
