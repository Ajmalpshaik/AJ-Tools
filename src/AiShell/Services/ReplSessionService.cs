#region Metadata
/*
 * Tool Name     : C#
 * File Name     : ReplSessionService.cs
 * Purpose       : Runs one line of C# at a time against the live Revit document and keeps the
 *                 resulting variables/imports alive for the next line - the actual "interactive
 *                 shell" piece (same idea as RevitPythonShell's IronPython console), sitting next
 *                 to the existing AI-authored full-script Run/RevitExecutionService path.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2026-07-21
 * Last Updated  : 2026-07-21
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, RoslynService
 *
 * Input         : One line/statement of C# text
 * Output        : CodeExecutionResult (success/output or error); session state carried internally
 *
 * Notes         :
 * - Each line runs in its own Transaction (not a TransactionGroup like RevitExecutionService) so a
 *   change made on one line is committed and visible to the next line, matching how an interactive
 *   shell is expected to behave. A failed line rolls back its own transaction and leaves the
 *   session's variables exactly as they were before it. Because of this, a typed line must NOT open
 *   its own Transaction (Revit does not support nesting one Transaction inside another) - that is
 *   the deliberate trade-off of an auto-committing console versus the whole-script Run path.
 * - The session automatically resets if the active document changes between lines (a stale
 *   ScriptState would otherwise hold a reference to a document that is no longer open).
 * - Re-entrancy guarded the same way as RevitExecutionService: a second line raised while one is
 *   still running returns a clear busy error instead of corrupting shared state.
 * - Loop protection and the hard Wait() backstop mirror RevitExecutionService for the same reason
 *   (Roslyn's RunAsync/ContinueWithAsync can block synchronously for non-async script code) - see
 *   that file's notes for what this can and cannot cancel. The timeout here is much shorter since a
 *   console line is meant to be quick, not a whole script.
 * - _globals is a session field, not a local: ScriptState.ContinueWithAsync has no globals
 *   parameter at all - it silently keeps reusing whatever globals object was passed to the very
 *   first RunAsync call in the chain. A fresh RevitScriptGlobals built on every call would be dead
 *   for every line after the first, and worse, every continued line would keep checking the FIRST
 *   line's CancellationToken - tied to a CancellationTokenSource already disposed by that line's
 *   `using` block, which stops its timer without ever firing, so LoopProtectionRewriter's checks
 *   would never see it as cancelled again. Mutating _globals' properties before each line (instead
 *   of replacing the object) keeps the same globals identity Roslyn expects while still giving each
 *   line a live, non-disposed token.
 *
 * Changelog     :
 * v1.0.1 (2026-07-21) - Two review fixes: (1) _globals is now a persisted, mutated-in-place session
 *                       field instead of a fresh local per call - fixes continuation lines silently
 *                       running against the first line's disposed CancellationToken (loop protection
 *                       was effectively dead past line 1). (2) Added an outer catch-all around
 *                       Execute() - previously an exception thrown before the inner try (e.g. from
 *                       Transaction construction/Start()) would leave the TaskCompletionSource never
 *                       completed, hanging the whole pane in IsBusy forever (same failure mode
 *                       RevitExecutionService v1.2.0 already hardened against).
 * v1.0.0 (2026-07-21) - Initial release: interactive line-by-line C# console with persisted state.
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
using Microsoft.CodeAnalysis.Scripting;
using AJTools.AiShell.Models;

namespace AJTools.AiShell.Services
{
    public class ReplSessionService : IExternalEventHandler
    {
        private readonly RoslynService _roslynService;
        private readonly ExternalEvent _externalEvent;
        private readonly object _lock = new object();

        private string _codeToExecute;
        private TaskCompletionSource<CodeExecutionResult> _tcs;
        private volatile bool _isRunning;

        private ScriptState<object> _state;
        private Document _sessionDocument;
        private RevitScriptGlobals _globals;

        // A console line is meant to be a quick, single statement/expression - a much shorter budget
        // than a whole AI-generated script gets in RevitExecutionService.
        private static readonly TimeSpan ReplTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan HardWaitTimeout = ReplTimeout + TimeSpan.FromSeconds(10);

        public ReplSessionService(RoslynService roslynService)
        {
            _roslynService = roslynService;
            _externalEvent = ExternalEvent.Create(this);
        }

        public Task<CodeExecutionResult> ExecuteAsync(string code)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    return Task.FromResult(new CodeExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Another console line is still running. Wait for it to finish."
                    });
                }

                _isRunning = true;
                _tcs = new TaskCompletionSource<CodeExecutionResult>();
                _codeToExecute = code;
            }

            _externalEvent.Raise();
            return _tcs.Task;
        }

        /// <summary>Clears the session's variables. Called from "Reset Session" and automatically
        /// when the active document changes.</summary>
        public void ResetSession()
        {
            lock (_lock)
            {
                _state = null;
                _sessionDocument = null;
                _globals = null;
            }
        }

        public void Execute(UIApplication app)
        {
            string code;
            TaskCompletionSource<CodeExecutionResult> tcs;
            lock (_lock)
            {
                code = _codeToExecute;
                tcs = _tcs;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    tcs.TrySetResult(new CodeExecutionResult { Success = false, ErrorMessage = "Nothing to run." });
                    return;
                }

                var uidoc = app.ActiveUIDocument;
                if (uidoc == null || uidoc.Document == null)
                {
                    tcs.TrySetResult(new CodeExecutionResult { Success = false, ErrorMessage = "No active document is open in Revit. Please open a project first." });
                    return;
                }

                var doc = uidoc.Document;
                bool sessionWasReset = false;
                if (_sessionDocument != null && !ReferenceEquals(_sessionDocument, doc))
                {
                    _state = null;
                    _globals = null;
                    sessionWasReset = true;
                }
                _sessionDocument = doc;

                using (var timeoutCts = new CancellationTokenSource(ReplTimeout))
                {
                    // Mutate the SAME globals instance in place rather than building a new one -
                    // ContinueWithAsync has no globals parameter, so anything past the first line
                    // would otherwise keep running against a stale, already-disposed
                    // CancellationTokenSource (see the class notes above).
                    if (_globals == null)
                    {
                        _globals = new RevitScriptGlobals
                        {
                            UIApplication = app,
                            Application = app.Application,
                            UIDocument = uidoc,
                            Document = doc,
                            CancellationToken = timeoutCts.Token,
                            ReportProgress = null
                        };
                    }
                    else
                    {
                        _globals.UIDocument = uidoc;
                        _globals.CancellationToken = timeoutCts.Token;
                    }

                    using (var tx = new Transaction(doc, "AJ AI Console"))
                    {
                        tx.Start();

                        ReplLineResult lineResult;
                        try
                        {
                            var task = _roslynService.ExecuteReplLineAsync(code, _state, _globals, timeoutCts.Token);
                            bool completed = task.Wait(HardWaitTimeout);

                            if (!completed)
                            {
                                tx.RollBack();
                                tcs.TrySetResult(new CodeExecutionResult
                                {
                                    Success = false,
                                    ErrorMessage = $"This line did not finish within {HardWaitTimeout.TotalSeconds:0} seconds. It may still be running in the background. Nothing was committed."
                                });
                                return;
                            }

                            lineResult = task.Result;
                        }
                        catch (Exception ex)
                        {
                            try { tx.RollBack(); } catch { /* transaction may already be in a terminal state */ }
                            tcs.TrySetResult(new CodeExecutionResult
                            {
                                Success = false,
                                ErrorMessage = ex.InnerException?.Message ?? ex.Message,
                                Exception = ex.InnerException ?? ex
                            });
                            return;
                        }

                        if (lineResult.Success)
                        {
                            tx.Commit();
                            _state = lineResult.NewState;
                            app.ActiveUIDocument?.RefreshActiveView();

                            string output = lineResult.Output;
                            if (sessionWasReset)
                            {
                                output = "(session reset - active document changed)\n" + (output ?? string.Empty);
                            }
                            tcs.TrySetResult(new CodeExecutionResult { Success = true, Output = output });
                        }
                        else
                        {
                            tx.RollBack();
                            // _state is left exactly as it was before this failed line - a typo should
                            // not lose the session's existing variables.
                            tcs.TrySetResult(new CodeExecutionResult { Success = false, ErrorMessage = lineResult.ErrorMessage });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Catches anything thrown before/between the inner try blocks above (e.g. Transaction
                // construction or Start() failing) - without this, such an exception would propagate
                // out of Execute() with tcs never completed, hanging the whole pane in IsBusy forever
                // (the same failure mode RevitExecutionService v1.2.0 already hardened against).
                tcs?.TrySetResult(new CodeExecutionResult
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred: " + ex.Message,
                    Exception = ex
                });
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                }
            }
        }

        public string GetName() => "AJ AI Console Session";
    }
}
