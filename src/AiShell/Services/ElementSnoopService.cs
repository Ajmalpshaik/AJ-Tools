#region Metadata
/*
 * Tool Name     : C#
 * File Name     : ElementSnoopService.cs
 * Purpose       : Reads every parameter (instance + type) of the currently selected element(s) and
 *                 formats them into a plain-text dump for the Output panel - a no-code way to see
 *                 "what parameters does this thing have", the same job RevitPythonShell's
 *                 lookup()/RevitLookup integration does for a modeler exploring the API.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-07-21
 * Last Updated  : 2026-07-23
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : None (reads the current selection on the active UIDocument)
 * Output        : Plain-text parameter dump string
 *
 * Notes         :
 * - Read-only: never opens a Transaction, never modifies the model.
 * - Detail is capped at MaxSnoopElements so selecting hundreds of elements doesn't flood the
 *   Output panel - same "cap detail, still show a total" idea RevitContextExtractionService
 *   already uses for the AI-context summary.
 * - Every per-parameter/per-field read is individually try/caught so one odd parameter never drops
 *   the rest of the dump.
 *
 * Changelog     :
 * v1.1.0 (2026-07-23) - Added the same re-entrancy guard RevitExecutionService/ReplSessionService
 *                       already use: a second SnoopSelectionAsync() call while one is still running
 *                       now returns immediately instead of overwriting the shared _tcs field, which
 *                       could otherwise leave an earlier caller's Task pending forever if Revit
 *                       coalesced the two ExternalEvent.Raise() calls into one Execute().
 * v1.0.0 (2026-07-21) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.AiShell.Services
{
    public class ElementSnoopService : IExternalEventHandler
    {
        private const int MaxSnoopElements = 5;

        private readonly ExternalEvent _externalEvent;
        private readonly object _lock = new object();
        private TaskCompletionSource<string> _tcs;

        // Same coalescing hazard RevitExecutionService/ReplSessionService already guard against:
        // ExternalEvent.Raise() can coalesce two rapid calls into a single Execute() invocation,
        // which would silently overwrite _tcs and leave an earlier caller's Task pending forever.
        private volatile bool _isRunning;

        public ElementSnoopService()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public Task<string> SnoopSelectionAsync()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    return Task.FromResult("Another Snoop is already running - try again in a moment.");
                }

                _isRunning = true;
                _tcs = new TaskCompletionSource<string>();
            }

            _externalEvent.Raise();
            return _tcs.Task;
        }

        public void Execute(UIApplication app)
        {
            TaskCompletionSource<string> tcs;
            lock (_lock)
            {
                tcs = _tcs;
            }

            try
            {
                var uidoc = app.ActiveUIDocument;
                if (uidoc == null || uidoc.Document == null)
                {
                    tcs.TrySetResult("No active document is open in Revit.");
                    return;
                }

                var doc = uidoc.Document;
                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 0)
                {
                    tcs.TrySetResult("Nothing selected. Select one or more elements in Revit, then click Snoop Selection again.");
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Snoop: {selectedIds.Count} element(s) selected.");

                int shown = 0;
                foreach (var id in selectedIds)
                {
                    if (shown >= MaxSnoopElements)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"...and {selectedIds.Count - shown} more (showing the first {MaxSnoopElements} only).");
                        break;
                    }

                    Element elem;
                    try { elem = doc.GetElement(id); }
                    catch { continue; }
                    if (elem == null) continue;

                    AppendElementDump(sb, doc, elem);
                    shown++;
                }

                tcs.TrySetResult(sb.ToString().Trim());
            }
            catch (Exception ex)
            {
                tcs.TrySetResult($"Snoop failed: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                }
            }
        }

        private static void AppendElementDump(StringBuilder sb, Document doc, Element elem)
        {
            sb.AppendLine();
            sb.AppendLine("========================================");

            try { sb.AppendLine($"Id: {elem.Id.IntValue()}   Category: {elem.Category?.Name ?? "(none)"}"); }
            catch { /* best-effort header */ }

            try { sb.AppendLine($"Class: {elem.GetType().Name}   Name: {SafeName(elem)}"); }
            catch { /* best-effort header */ }

            try
            {
                var typeId = elem.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    var typeElem = doc.GetElement(typeId);
                    if (typeElem != null) sb.AppendLine($"Type: {SafeName(typeElem)} (Id {typeId.IntValue()})");
                }
            }
            catch { /* not every element has a type */ }

            try
            {
                var levelId = elem.LevelId;
                if (levelId != null && levelId != ElementId.InvalidElementId)
                {
                    var level = doc.GetElement(levelId) as Level;
                    if (level != null) sb.AppendLine($"Level: {level.Name}");
                }
            }
            catch { /* not every element exposes a meaningful LevelId */ }

            try
            {
                if (doc.IsWorkshared)
                {
                    var workset = doc.GetWorksetTable().GetWorkset(elem.WorksetId);
                    if (workset != null) sb.AppendLine($"Workset: {workset.Name}");
                }
            }
            catch { /* workset info is a bonus, not essential */ }

            sb.AppendLine();
            sb.AppendLine("-- Instance Parameters --");
            AppendParameters(sb, doc, elem);

            try
            {
                var typeId = elem.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    var typeElem = doc.GetElement(typeId);
                    if (typeElem != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine("-- Type Parameters --");
                        AppendParameters(sb, doc, typeElem);
                    }
                }
            }
            catch { /* type parameter section is best-effort */ }
        }

        private static void AppendParameters(StringBuilder sb, Document doc, Element elem)
        {
            IList<Parameter> parameters;
            try
            {
                parameters = elem.GetOrderedParameters();
            }
            catch
            {
                sb.AppendLine("(parameters unavailable for this element)");
                return;
            }

            if (parameters == null || parameters.Count == 0)
            {
                sb.AppendLine("(none)");
                return;
            }

            foreach (var p in parameters.OrderBy(param => param.Definition?.Name ?? string.Empty))
            {
                try
                {
                    string name = p.Definition?.Name ?? "(unnamed)";
                    string value = FormatParameterValue(p, doc);
                    sb.AppendLine($"  {name} = {value}");
                }
                catch
                {
                    // One odd parameter (e.g. an unresolvable formula) must not drop the rest.
                }
            }
        }

        private static string FormatParameterValue(Parameter p, Document doc)
        {
            if (!p.HasValue) return "(not set)";

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        return p.AsString() ?? "(empty)";
                    case StorageType.Integer:
                        return p.AsValueString() ?? p.AsInteger().ToString();
                    case StorageType.Double:
                        return p.AsValueString() ?? p.AsDouble().ToString("0.####");
                    case StorageType.ElementId:
                        var id = p.AsElementId();
                        if (id == null || id == ElementId.InvalidElementId) return "(none)";
                        return $"{SafeName(doc.GetElement(id))} (Id {id.IntValue()})";
                    default:
                        return p.AsValueString() ?? "(unset)";
                }
            }
            catch
            {
                return "(unreadable)";
            }
        }

        private static string SafeName(Element elem)
        {
            if (elem == null) return "(none)";
            try { return string.IsNullOrWhiteSpace(elem.Name) ? $"(unnamed {elem.GetType().Name})" : elem.Name; }
            catch { return "(unnamed)"; }
        }

        public string GetName() => "AJ AI Snoop Selection";
    }
}
