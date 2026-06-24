using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AJTools.GeminiShell.Models;
using AJTools.GeminiShell.Helpers;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.GeminiShell.Services
{
    public class RoslynService
    {
        public async Task<CodeExecutionResult> ExecuteAsync(string code, RevitScriptGlobals globals)
        {
            var result = new CodeExecutionResult();

            try
            {
                var options = ScriptOptions.Default
                    .WithReferences(
                        typeof(object).Assembly,                            // mscorlib
                        typeof(Enumerable).Assembly,                        // System.Core
                        typeof(System.Collections.Generic.List<>).Assembly,  // mscorlib
                        typeof(System.IO.File).Assembly,                     // mscorlib (System.IO)
                        typeof(System.Text.StringBuilder).Assembly,          // mscorlib (System.Text)
                        typeof(System.Text.RegularExpressions.Regex).Assembly, // System
                        typeof(System.Net.WebClient).Assembly,               // System
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

                // Apply Infinite Loop Protection
                var parseOptions = new CSharpParseOptions(kind: SourceCodeKind.Script);
                var syntaxTree = CSharpSyntaxTree.ParseText(code, parseOptions);
                var rewriter = new LoopProtectionRewriter();
                var safeRoot = rewriter.Visit(syntaxTree.GetRoot());
                var safeCode = safeRoot.ToFullString();

                var script = CSharpScript.Create(safeCode, options, typeof(RevitScriptGlobals));
                
                var diagnostics = script.Compile();
                if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
                {
                    result.Success = false;
                    result.ErrorMessage = string.Join("\n", diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).Select(d => d.ToString()));
                    return result;
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
    }
}
