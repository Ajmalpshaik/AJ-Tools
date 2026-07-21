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
 * Version       : 1.0.0
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
 *
 * Changelog     :
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
                    sessionWasReset = true;
                }
                _sessionDocument = doc;

                using (var timeoutCts = new CancellationTokenSource(ReplTimeout))
                {
                    var globals = new RevitScriptGlobals
                    {
                        UIApplication = app,
                        Application = app.Application,
                        UIDocument = uidoc,
                        Document = doc,
                        CancellationToken = timeoutCts.Token,
                        ReportProgress = null
                    };

                    using (var tx = new Transaction(doc, "AJ AI Console"))
                    {
                        tx.Start();

                        ReplLineResult lineResult;
                        try
                        {
                            var task = _roslynService.ExecuteReplLineAsync(code, _state, globals, timeoutCts.Token);
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
