#region Metadata
/*
 * Tool Name     : AJ AI (Gemini Shell)
 * File Name     : RevitExecutionService.cs
 * Purpose       : Runs AI-generated C# scripts against the live Revit document on the correct
 *                 Revit API thread via ExternalEvent, wrapped in a single-undo TransactionGroup.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-01-01
 * Last Updated  : 2026-07-17
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, RoslynService
 *
 * Input         : C# script text + progress callback + cancellation token
 * Output        : CodeExecutionResult (success/output or error), model changes committed or rolled back
 *
 * Notes         :
 * - Re-entrancy guarded: a second call while one is running returns a clear busy error instead
 *   of silently overwriting shared state (ExternalEvent.Raise() can coalesce rapid calls).
 * - A running script can only be interrupted at loop checkpoints (see LoopProtectionRewriter) or
 *   after a 60s hard timeout — a single long-running Revit API call cannot be cancelled mid-flight.
 *
 * Changelog     :
 * v1.2.0 (2026-07-17) - Guarded the failure-path group.RollBack() call: if a script's own
 *                       TransactionGroup.Commit() throws, the catch block used to call RollBack()
 *                       unguarded - if that secondary call also threw (group already in a terminal
 *                       state), the exception escaped before tcs.TrySetResult() ran, leaving the
 *                       caller's Task pending forever and the AJ AI pane stuck on IsBusy until the
 *                       add-in was restarted. Now the secondary failure is swallowed and the
 *                       original error is still always reported. NOTE: this pass did not add a hard
 *                       timeout to the `task.Wait()` call below - doing so safely would need to
 *                       confirm Roslyn's CSharpScript.RunAsync threading model against the Revit API
 *                       single-thread requirement first, which needs a real Revit/Visual Studio
 *                       environment to verify; flagged for a follow-up, not guessed at here.
 * v1.0.0 (2026-01-01) - Initial release.
 * v1.1.0 (2026-07-01) - Added re-entrancy guard, wired Stop-button cancellation token through to
 *                       the script's CancellationToken, added mandatory metadata block.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Threading;
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

        // Prevents a second script from being raised while one is still running. Without this,
        // Revit can coalesce two rapid ExternalEvent.Raise() calls into a single Execute()
        // invocation, silently overwriting the first caller's code/callback and leaving its
        // Task pending forever.
        private volatile bool _isRunning;

        public RevitExecutionService(RoslynService roslynService)
        {
            _roslynService = roslynService;
            _externalEvent = ExternalEvent.Create(this);
        }

        // Loop-based scripts are auto-cancelled after this long even if the user never presses Stop
        // (backstop against an AI-generated infinite loop). See LoopProtectionRewriter.
        private static readonly TimeSpan MaxLoopRuntime = TimeSpan.FromSeconds(60);

        private CancellationToken _externalCancellationToken;

        public Task<CodeExecutionResult> ExecuteAsync(string code, Action<int, string> progressCallback = null, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    var busyResult = new CodeExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Another script is still running. Wait for it to finish or press Stop before running another."
                    };
                    return Task.FromResult(busyResult);
                }

                _isRunning = true;
                _tcs = new TaskCompletionSource<CodeExecutionResult>();
                _codeToExecute = code;
                _progressCallback = progressCallback;
                _externalCancellationToken = cancellationToken;
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
            CancellationToken externalToken;
            lock (_lock)
            {
                code = _codeToExecute;
                progressCallback = _progressCallback;
                tcs = _tcs;
                externalToken = _externalCancellationToken;
            }

            try
            {
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

                using (var timeoutCts = new CancellationTokenSource(MaxLoopRuntime))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, externalToken))
                {
                    var globals = new RevitScriptGlobals
                    {
                        UIApplication = app,
                        Application = app.Application,
                        UIDocument = uidoc,
                        Document = uidoc.Document,
                        CancellationToken = linkedCts.Token,
                        ReportProgress = progressCallback
                    };

                    // Use TransactionGroup so users can paste external scripts with their own transactions
                    using (var group = new TransactionGroup(globals.Document, "Gemini AI Script Execution"))
                    {
                        group.Start();

                        try
                        {
                            // Note: CSharpScript.RunAsync is asynchronous, but we must block here on the Revit thread
                            // to safely access the Revit API objects. This can only interrupt loop-based scripts
                            // (see LoopProtectionRewriter) — a script stuck in a single long-running Revit API
                            // call cannot be cancelled and will run to completion or throw its own exception.
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
                            try
                            {
                                group.RollBack();
                            }
                            catch
                            {
                                // The group may already be in a terminal state (e.g. Commit() itself
                                // just failed) - nothing more can be done to it. Still fall through and
                                // report the original error below so the caller's Task always completes;
                                // swallowing it here would otherwise leave the AJ AI pane hung forever.
                            }

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
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                }
            }
        }

        public string GetName() => "Gemini Shell Revit Execution Service";
    }
}
