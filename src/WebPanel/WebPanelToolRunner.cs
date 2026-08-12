#region Metadata
/*
 * Tool Name     : Web Panel (tool runner)
 * File Name     : WebPanelToolRunner.cs
 * Purpose       : Runs an AJ Tools tool on Revit's own UI thread on behalf of a browser request,
 *                 and holds the fixed registry of which tools the Web Panel is allowed to run at
 *                 all. WebPanelService (the HTTP side) never touches the Revit API itself - it
 *                 hands a tool id to this class and waits for the result.
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
 * Dependencies  : Autodesk Revit API, ValidationHelper, UnhideAllService
 *
 * Input         : A tool id string, already authenticated by WebPanelService.
 * Output        : WebPanelToolResult - success flag plus the same report text the ribbon shows.
 *
 * Notes         :
 * - THE SECURITY MODEL OF THIS FIRST VERSION IS "NAMES, NOT CODE". The browser sends a tool id
 *   ("unhide-all") and nothing else. It cannot send C#, a script, or a file path, so a hostile page
 *   - or a hacked website later serving the panel - can at worst trigger a tool that is already
 *   compiled into this add-in and already reachable from the ribbon. This is deliberately narrower
 *   than the AJ AI bridge, which does accept code (guarded by GeneratedCodeSafetyValidator). If the
 *   Web Panel is ever extended to run downloaded tool code, that step needs its own signature check
 *   first - do not simply widen this registry to "any code".
 * - A web request arrives on a thread pool thread. The Revit API refuses any call from a thread
 *   other than Revit's own, so every tool is dispatched through ExternalEvent, exactly the way
 *   RevitExecutionService does it for the AI shell.
 * - Re-entrancy guard, same reason as RevitExecutionService: Revit can coalesce two rapid Raise()
 *   calls into a single Execute(), which would silently drop the first caller's request and leave
 *   its Task pending forever. Two browser tabs clicking at once is an easy way to hit this.
 * - Nothing here may show a dialog. The person who clicked is looking at a browser; a TaskDialog
 *   would appear on the Revit screen and block Revit until someone walked over and clicked it.
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Initial release. Registry holds the read-only "context" probe plus the first
 *                       real tool, Unhide All, which calls the shared UnhideAllService.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.UnhideAll;
using AJTools.Utils;

namespace AJTools.WebPanel
{
    /// <summary>What a Web Panel run produced, in the form the browser displays it.</summary>
    public class WebPanelToolResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static WebPanelToolResult Ok(string message)
        {
            return new WebPanelToolResult { Success = true, Message = message };
        }

        public static WebPanelToolResult Fail(string message)
        {
            return new WebPanelToolResult { Success = false, Message = message };
        }
    }

    /// <summary>One entry in the fixed list of tools the Web Panel may run.</summary>
    public class WebPanelTool
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Panel { get; set; }
        public string Description { get; set; }

        /// <summary>True for probes that only read. Skips the editable-document check.</summary>
        public bool IsReadOnly { get; set; }

        public Func<UIApplication, WebPanelToolResult> Run { get; set; }
    }

    public class WebPanelToolRunner : IExternalEventHandler
    {
        private readonly ExternalEvent _externalEvent;
        private readonly object _lock = new object();

        private string _pendingToolId;
        private TaskCompletionSource<WebPanelToolResult> _tcs;
        private volatile bool _isRunning;

        public WebPanelToolRunner()
        {
            // Must be created on Revit's UI thread (App.OnStartup does this), same as every other
            // ExternalEvent in the project.
            _externalEvent = ExternalEvent.Create(this);
        }

        #region Tool registry - the ONLY things a browser can trigger

        private static readonly List<WebPanelTool> RegisteredTools = new List<WebPanelTool>
        {
            new WebPanelTool
            {
                Id = "context",
                Name = "Session",
                Panel = "System",
                Description = "Revit version, model and active view.",
                IsReadOnly = true,
                Run = ReadContext
            },
            new WebPanelTool
            {
                Id = "unhide-all",
                Name = "Unhide All",
                Panel = "View",
                Description = "Restore hidden elements in the active view.",
                Run = RunUnhideAll
            }
        };

        public static IEnumerable<WebPanelTool> Tools
        {
            get { return RegisteredTools; }
        }

        private static WebPanelTool FindTool(string id)
        {
            foreach (var tool in RegisteredTools)
            {
                if (string.Equals(tool.Id, id, StringComparison.OrdinalIgnoreCase)) return tool;
            }
            return null;
        }

        #endregion

        #region The tools themselves

        private static WebPanelToolResult ReadContext(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
                return WebPanelToolResult.Fail("No model is open in Revit.");

            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            string modelTitle = string.IsNullOrWhiteSpace(doc.Title) ? "(unsaved)" : doc.Title;
            string viewName = view == null ? "(none)" : view.Name;
            string viewType = view == null ? string.Empty : view.ViewType.ToString();

            // Pipe-separated rather than JSON: WebPanelService owns all JSON shaping, so this stays
            // a plain string like every other tool's Message.
            return WebPanelToolResult.Ok(
                app.Application.VersionNumber + "|" + modelTitle + "|" + viewName + "|" + viewType);
        }

        private static WebPanelToolResult RunUnhideAll(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            string message;

            if (!ValidationHelper.ValidateUIDocumentAndView(uidoc, out message))
                return WebPanelToolResult.Fail(message);

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                return WebPanelToolResult.Fail(message);

            // Exactly the same call the ribbon button makes.
            UnhideAllResult result = UnhideAllService.Run(doc, doc.ActiveView);
            return WebPanelToolResult.Ok(result.Summary);
        }

        #endregion

        #region ExternalEvent plumbing

        /// <summary>
        /// Queues a tool to run on Revit's UI thread and completes when it has finished. Safe to call
        /// from any thread - that is the entire point of it.
        /// </summary>
        public Task<WebPanelToolResult> RunAsync(string toolId)
        {
            var tool = FindTool(toolId);
            if (tool == null)
                return Task.FromResult(WebPanelToolResult.Fail("Unknown tool: " + toolId));

            lock (_lock)
            {
                if (_isRunning)
                {
                    return Task.FromResult(WebPanelToolResult.Fail(
                        "Another tool is still running in Revit. Wait for it to finish, then try again."));
                }

                _isRunning = true;
                _tcs = new TaskCompletionSource<WebPanelToolResult>();
                _pendingToolId = toolId;
            }

            _externalEvent.Raise();
            return _tcs.Task;
        }

        public void Execute(UIApplication app)
        {
            string toolId;
            TaskCompletionSource<WebPanelToolResult> tcs;

            lock (_lock)
            {
                toolId = _pendingToolId;
                tcs = _tcs;
            }

            if (tcs == null) return;

            try
            {
                var tool = FindTool(toolId);
                if (tool == null)
                {
                    tcs.TrySetResult(WebPanelToolResult.Fail("Unknown tool: " + toolId));
                    return;
                }

                if (app == null || app.ActiveUIDocument == null)
                {
                    tcs.TrySetResult(WebPanelToolResult.Fail("No model is open in Revit."));
                    return;
                }

                tcs.TrySetResult(tool.Run(app));
            }
            catch (Exception ex)
            {
                // Never let an exception escape into Revit's event loop - report it to the browser
                // instead, which is the only place anyone is looking.
                tcs.TrySetResult(WebPanelToolResult.Fail(ex.Message));
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                    _pendingToolId = null;
                    _tcs = null;
                }
            }
        }

        public string GetName()
        {
            return "AJ Tools Web Panel";
        }

        #endregion
    }
}
