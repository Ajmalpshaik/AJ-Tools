#region Metadata
/*
 * Tool Name     : AJ Tools Connector (script runner)
 * File Name     : ScriptRunner.cs
 * Purpose       : Runs a verified tool's C# on Revit's own UI thread and returns its report as text.
 *                 Only ever called with a tool that already passed ToolSignature.Verify.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-12
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, Microsoft.CodeAnalysis.CSharp.Scripting
 *
 * Input         : A ConnectorTool whose signature has already been checked.
 * Output        : ToolRunResult - success flag and the text the browser displays.
 *
 * Notes         :
 * - Signature checking does NOT happen here, on purpose. Verification belongs at the point code
 *   enters the system (ToolCatalogue), not at the point it runs, so there is exactly one gate to
 *   audit rather than a check scattered across call sites. Nothing may call this with an unverified
 *   tool - ConnectorServer only ever passes it something the catalogue accepted.
 * - A browser request arrives on a thread pool thread and the Revit API refuses any thread but its
 *   own, so work is dispatched through ExternalEvent. Same proven shape as AJ Tools' own
 *   RevitExecutionService.
 * - Re-entrancy guard: Revit can coalesce two rapid Raise() calls into one Execute(), which would
 *   drop the first request and leave its Task pending forever. Two browser tabs, or an impatient
 *   double click, reach that easily.
 * - Everything runs inside a TransactionGroup so a tool may open its own transactions (they almost
 *   all need to), and so a tool that throws part-way leaves nothing half-applied.
 * - A tool reports by returning a value - its last expression, or an explicit return. Nothing here
 *   shows a dialog: the person who clicked is looking at a browser, and a TaskDialog would appear on
 *   the Revit screen and block Revit until somebody physically walked over to it.
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace AJToolsConnector
{
    /// <summary>Handed to every tool script by these names, same as the AJ AI shell.</summary>
    public class ToolScriptGlobals
    {
        public Document Document { get; set; }
        public UIDocument UIDocument { get; set; }
        public Autodesk.Revit.ApplicationServices.Application Application { get; set; }
        public UIApplication UIApplication { get; set; }
    }

    public class ToolRunResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static ToolRunResult Ok(string message)
        {
            return new ToolRunResult { Success = true, Message = message };
        }

        public static ToolRunResult Fail(string message)
        {
            return new ToolRunResult { Success = false, Message = message };
        }
    }

    public class ScriptRunner : IExternalEventHandler
    {
        // Long enough for a full-model collector on a heavy project; short enough that a browser tab
        // is never left waiting on a script that will not finish.
        private static readonly TimeSpan MaxRuntime = TimeSpan.FromSeconds(75);

        private readonly ExternalEvent _externalEvent;
        private readonly object _lock = new object();

        private ConnectorTool _pending;
        private bool _pendingIsContext;
        private TaskCompletionSource<ToolRunResult> _tcs;
        private volatile bool _isRunning;

        public ScriptRunner()
        {
            // Must be constructed on Revit's UI thread - ConnectorApp.OnStartup does this.
            _externalEvent = ExternalEvent.Create(this);
        }

        /// <summary>Queues a verified tool and completes when Revit has finished with it.</summary>
        public Task<ToolRunResult> RunAsync(ConnectorTool tool)
        {
            if (tool == null || string.IsNullOrWhiteSpace(tool.Code))
                return Task.FromResult(ToolRunResult.Fail("That tool has no code to run."));

            return QueueAsync(tool, isContext: false);
        }

        /// <summary>
        /// Reads the current Revit version, model and view for the page header. Built in rather than
        /// published as a tool: it must work on a brand-new install before any tool exists, it only
        /// reads, and running it through Roslyn would pay a compile on every 15-second refresh.
        /// </summary>
        public Task<ToolRunResult> RunContextAsync()
        {
            return QueueAsync(null, isContext: true);
        }

        private Task<ToolRunResult> QueueAsync(ConnectorTool tool, bool isContext)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    return Task.FromResult(ToolRunResult.Fail(
                        "Another tool is still running in Revit. Wait for it to finish, then try again."));
                }

                _isRunning = true;
                _tcs = new TaskCompletionSource<ToolRunResult>();
                _pending = tool;
                _pendingIsContext = isContext;
            }

            _externalEvent.Raise();
            return _tcs.Task;
        }

        public void Execute(UIApplication app)
        {
            ConnectorTool tool;
            bool isContext;
            TaskCompletionSource<ToolRunResult> tcs;

            lock (_lock)
            {
                tool = _pending;
                isContext = _pendingIsContext;
                tcs = _tcs;
            }

            if (tcs == null) return;

            try
            {
                if (app == null || app.ActiveUIDocument == null || app.ActiveUIDocument.Document == null)
                {
                    tcs.TrySetResult(ToolRunResult.Fail("No model is open in Revit."));
                    return;
                }

                tcs.TrySetResult(isContext ? ReadContext(app) : RunOnRevitThread(app, tool));
            }
            catch (Exception ex)
            {
                // Nothing may escape into Revit's event loop - report it to the browser, the only
                // place anyone is watching.
                tcs.TrySetResult(ToolRunResult.Fail(Describe(ex)));
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                    _pending = null;
                    _pendingIsContext = false;
                    _tcs = null;
                }
            }
        }

        /// <summary>
        /// Pipe-separated rather than JSON: ConnectorServer owns all JSON shaping, so this stays a
        /// plain string like every tool's own report.
        /// </summary>
        private static ToolRunResult ReadContext(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var view = doc.ActiveView;

            string modelTitle = string.IsNullOrWhiteSpace(doc.Title) ? "(unsaved)" : doc.Title;
            string viewName = view == null ? "(none)" : view.Name;
            string viewType = view == null ? string.Empty : view.ViewType.ToString();

            return ToolRunResult.Ok(
                app.Application.VersionNumber + "|" + modelTitle + "|" + viewName + "|" + viewType);
        }

        private static ToolRunResult RunOnRevitThread(UIApplication app, ConnectorTool tool)
        {
            var uidoc = app.ActiveUIDocument;
            var globals = new ToolScriptGlobals
            {
                UIApplication = app,
                Application = app.Application,
                UIDocument = uidoc,
                Document = uidoc.Document
            };

            var options = BuildScriptOptions();

            // A group, not a transaction: tools open their own transactions, and this makes the whole
            // tool one undo step in Revit rather than several.
            using (var group = new TransactionGroup(globals.Document, "AJ Tools Connector: " + tool.Name))
            {
                group.Start();

                try
                {
                    var task = CSharpScript.RunAsync<object>(tool.Code, options, globals);

                    // Blocking on the Revit thread is required: the script touches Revit API objects,
                    // which are only valid here.
                    if (!task.Wait(MaxRuntime))
                    {
                        // Not committing or rolling back explicitly - if the script really is still
                        // running, touching the group from here while it may also be touching it is
                        // unsafe. Letting the using-block dispose roll it back is the safer contract.
                        return ToolRunResult.Fail(
                            "\"" + tool.Name + "\" did not finish within " + MaxRuntime.TotalSeconds +
                            " seconds and was abandoned. Nothing was saved.");
                    }

                    object value = task.Result.ReturnValue;
                    group.Assimilate();

                    string text = value == null ? "Done." : value.ToString();
                    if (string.IsNullOrWhiteSpace(text)) text = "Done.";

                    return ToolRunResult.Ok(text);
                }
                catch (Exception ex)
                {
                    // The group rolls back on dispose, so the model is left as it was found.
                    return ToolRunResult.Fail(Describe(ex));
                }
            }
        }

        private static ScriptOptions BuildScriptOptions()
        {
            // References taken from the Revit assemblies already loaded in this process, so they
            // always match the Revit version actually running rather than a build-time assumption.
            return ScriptOptions.Default
                .AddReferences(
                    typeof(Document).Assembly,
                    typeof(UIDocument).Assembly,
                    typeof(object).Assembly,
                    typeof(System.Linq.Enumerable).Assembly)
                .AddImports(
                    "System",
                    "System.Linq",
                    "System.Collections.Generic",
                    "Autodesk.Revit.DB",
                    "Autodesk.Revit.UI");
        }

        /// <summary>
        /// Unwraps the AggregateException that Task.Wait always throws, so the browser shows the real
        /// problem rather than "One or more errors occurred."
        /// </summary>
        private static string Describe(Exception ex)
        {
            var aggregate = ex as AggregateException;
            if (aggregate != null && aggregate.InnerExceptions.Count > 0)
                ex = aggregate.InnerExceptions[0];

            var compileError = ex as CompilationErrorException;
            if (compileError != null)
                return "That tool did not compile:\n" + string.Join("\n", compileError.Diagnostics);

            return ex.Message;
        }

        public string GetName()
        {
            return "AJ Tools Connector";
        }
    }
}
