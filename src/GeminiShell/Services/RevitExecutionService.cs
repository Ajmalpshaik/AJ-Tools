using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using AJTools.GeminiShell.Models;

namespace AJTools.GeminiShell.Services
{
    public class RevitExecutionService : IExternalEventHandler
    {
        private readonly RoslynService _roslynService;
        private readonly ExternalEvent _externalEvent;
        private readonly object _lock = new object();
        
        // State for execution
        private string _codeToExecute;
        private Action<int, string> _progressCallback;
        private TaskCompletionSource<CodeExecutionResult> _tcs;

        public RevitExecutionService(RoslynService roslynService)
        {
            _roslynService = roslynService;
            _externalEvent = ExternalEvent.Create(this);
        }

        public Task<CodeExecutionResult> ExecuteAsync(string code, Action<int, string> progressCallback = null)
        {
            lock (_lock)
            {
                _tcs = new TaskCompletionSource<CodeExecutionResult>();
                _codeToExecute = code;
                _progressCallback = progressCallback;
            }
            
            // Raise the external event
            _externalEvent.Raise();

            return _tcs.Task;
        }

        public void Execute(UIApplication app)
        {
            string code;
            Action<int, string> progressCallback;
            TaskCompletionSource<CodeExecutionResult> tcs;
            lock (_lock)
            {
                code = _codeToExecute;
                progressCallback = _progressCallback;
                tcs = _tcs;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                tcs.TrySetResult(new CodeExecutionResult { Success = false, ErrorMessage = "No code to execute." });
                return;
            }

            var uidoc = app.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                tcs.TrySetResult(new CodeExecutionResult { Success = false, ErrorMessage = "No active document is open in Revit. Please open a project first." });
                return;
            }

            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                var globals = new RevitScriptGlobals
                {
                    UIApplication = app,
                    Application = app.Application,
                    UIDocument = uidoc,
                    Document = uidoc.Document,
                    CancellationToken = cts.Token,
                    ReportProgress = progressCallback
                };

            // Use TransactionGroup so users can paste external scripts with their own transactions
            using (var group = new TransactionGroup(globals.Document, "Gemini AI Script Execution"))
            {
                group.Start();

                try
                {
                    // Note: CSharpScript.RunAsync is asynchronous, but we must block here on the Revit thread
                    // to safely access the Revit API objects.
                    var task = _roslynService.ExecuteAsync(code, globals);
                    task.Wait();
                    
                    var result = task.Result;

                    if (result.Success)
                    {
                        group.Commit(); // Commit the transaction group so changes appear
                        app.ActiveUIDocument?.RefreshActiveView();
                    }
                    else
                    {
                        group.RollBack();
                    }

                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    group.RollBack();
                    tcs.TrySetResult(new CodeExecutionResult
                    {
                        Success = false,
                        ErrorMessage = ex.InnerException?.Message ?? ex.Message,
                        Exception = ex.InnerException ?? ex
                    });
                }
                }
            }
        }

        public string GetName() => "Gemini Shell Revit Execution Service";
    }
}
