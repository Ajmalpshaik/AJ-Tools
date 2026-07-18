#region Metadata
/*
 * Tool Name     : AJ AI (Gemini Shell)
 * File Name     : RevitExecutionService.cs
 * Purpose       : Runs AI-generated C# scripts against the live Revit document on the correct
 *                 Revit API thread via ExternalEvent, wrapped in a single-undo TransactionGroup.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.3.0
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
 * - A running script can only be interrupted at loop checkpoints (see LoopProtectionRewriter),
 *   after the 60s soft cancellation deadline, or the 80s hard task.Wait() backstop below - a
 *   script that never yields at a loop checkpoint (a goto-loop, Thread.Sleep, or a single very
 *   long Revit API call) cannot be forcibly killed; the hard backstop only stops the Revit UI
 *   thread from waiting on it forever, it does not guarantee the runaway work actually stops.
 *
 * Changelog     :
 * v1.3.0 (2026-07-17) - Added a hard backstop to the blocking task.Wait() call (MaxLoopRuntime +
 *                       20s grace): previously this had no timeout of its own at all, so a script
 *                       that never reached a LoopProtectionRewriter checkpoint (goto-loop,
 *                       Thread.Sleep, a single long-running call) could hang the Revit UI thread
 *                       indefinitely with no recovery short of killing Revit. On timeout, reports a
 *                       clear error and returns without touching the TransactionGroup directly -
 *                       letting its `using` block's Dispose() run the default roll-back-if-not-
 *                       assimilated behavior is safer than this thread reaching into it if the
 *                       script instance might still be executing. This narrows but does not fully
 *                       close the freeze risk: if Roslyn's CSharpScript.RunAsync truly blocks the
 *                       calling thread synchronously (the common case for non-async script code),
 *                       task.Wait() itself is never reached until the script returns, so this
 *                       backstop cannot help in that specific case - full isolation would need a
 *                       separate process/AppDomain, already noted as out of scope for this file.
 * v1.2.0 (2026-07-17) - Guarded the failure-path group.RollBack() call: if a script's own
 *                       TransactionGroup.Commit() throws, the catch block used to call RollBack()
 *                       unguarded - if that secondary call also threw (group already in a terminal
 *                       state), the exception escaped before tcs.TrySetResult() ran, leaving the
 *                       caller's Task pending forever and the AJ AI pane stuck on IsBusy until the
 *                       add-in was restarted. Now the secondary failure is swallowed and the
 *                       original error is still always reported.
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

        // Hard backstop on the blocking Wait() below, on top of MaxLoopRuntime. Gives a script that
        // DOES respect the CancellationToken (see LoopProtectionRewriter) time to actually unwind
        // through nested calls before this gives up on it. A script that does NOT respect the token
        // (a goto-loop, Thread.Sleep, or a single very long Revit API call) cannot be forcibly killed
        // from here - .NET has no safe way to abort a running thread - so this only stops the Revit
        // UI thread from waiting forever; it does not guarantee the runaway work actually stops.
        private static readonly TimeSpan HardWaitTimeout = MaxLoopRuntime + TimeSpan.FromSeconds(20);

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
                            bool completed = task.Wait(HardWaitTimeout);

                            if (!completed)
                            {
                                // Deliberately not calling group.Commit()/RollBack() here: if the
                                // script really is still running, touching the group from this thread
                                // while it might still be touched by the script is not safe. Letting
                                // the `using (group)` block below run Dispose()'s default roll-back
                                // (matching Transaction's own IDisposable contract) is the safer choice.
                                tcs.TrySetResult(new CodeExecutionResult
                                {
                                    Success = false,
                                    ErrorMessage = $"The script did not finish within {HardWaitTimeout.TotalSeconds:0} seconds and did not respond to Stop. " +
                                                   "It may still be running in the background. Any changes were not committed."
                                });
                                return;
                            }

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
