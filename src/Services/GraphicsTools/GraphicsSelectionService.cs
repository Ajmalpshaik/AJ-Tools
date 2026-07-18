#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : GraphicsSelectionService.cs
 * Purpose       : Shared command context creation, single-transaction execution, and preselection-first selection capture for all Graphics tools.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active Revit UI document and user selections.
 * Output        : Validated command context and distinct selected element ids.
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - Validates active view, editable document, and that graphics overrides are allowed before any command runs.
 * - ESC during a pick is caught and reported as a silent cancel.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Version-safe ElementId access; full metadata block.
 * v1.4.4 (2026-05-09) - Added active-view graphics override validation for all Graphics commands.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Models.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Services.GraphicsTools
{
    internal sealed class GraphicsCommandContext
    {
        internal GraphicsCommandContext(UIDocument uidocument, Document document, View activeView)
        {
            UIDocument = uidocument;
            Document = document;
            ActiveView = activeView;
        }

        internal UIDocument UIDocument { get; }

        internal Document Document { get; }

        internal View ActiveView { get; }
    }

    internal static class GraphicsCommandService
    {
        internal static Result TryCreateContext(
            ExternalCommandData commandData,
            string title,
            ref string message,
            out GraphicsCommandContext context)
        {
            context = null;

            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocumentAndView(uidoc, out message))
            {
                DialogHelper.ShowError(title, message);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                DialogHelper.ShowError(title, message);
                return Result.Cancelled;
            }

            View activeView = doc.ActiveView;
            if (!activeView.AreGraphicsOverridesAllowed())
            {
                message = "Graphics overrides are disabled for this view.";
                DialogHelper.ShowError(title, message);
                return Result.Cancelled;
            }

            context = new GraphicsCommandContext(uidoc, doc, activeView);
            return Result.Succeeded;
        }

        internal static GraphicsOperationSummary ExecuteSummaryTransaction(
            Document doc,
            string transactionName,
            Func<GraphicsOperationSummary> operation)
        {
            if (doc == null || operation == null)
                return new GraphicsOperationSummary();

            using (var transaction = new Transaction(doc, transactionName))
            {
                bool completed = false;

                try
                {
                    transaction.Start();
                    GraphicsOperationSummary summary = operation() ?? new GraphicsOperationSummary();

                    if (summary.HasChanges)
                        transaction.Commit();
                    else
                        transaction.RollBack();

                    completed = true;
                    return summary;
                }
                catch
                {
                    if (!completed)
                    {
                        try
                        {
                            transaction.RollBack();
                        }
                        catch
                        {
                            // Preserve the original operation error.
                        }
                    }

                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Unified result for capture operations that may be cancelled by the user.
    /// </summary>
    internal sealed class SelectionCaptureResult
    {
        public SelectionCaptureResult(IList<ElementId> elementIds, bool wasCancelled)
        {
            ElementIds = elementIds ?? new List<ElementId>();
            WasCancelled = wasCancelled;
        }

        public IList<ElementId> ElementIds { get; }

        public bool WasCancelled { get; }
    }

    /// <summary>
    /// Selection helper methods with preselection-first behavior.
    /// </summary>
    internal static class GraphicsSelectionService
    {
        public static SelectionCaptureResult GetPreselectedOrPromptElementIds(
            UIDocument uidoc,
            ISelectionFilter selectionFilter,
            string prompt,
            ICollection<ElementId> excludedElementIds = null)
        {
            if (uidoc == null || uidoc.Document == null)
            {
                return new SelectionCaptureResult(new List<ElementId>(), false);
            }

            var excluded = BuildExcludedSet(excludedElementIds);

            IList<ElementId> validPreselection = FilterDistinctElementIds(
                uidoc.Document,
                uidoc.Selection.GetElementIds(),
                selectionFilter,
                excluded);
            if (validPreselection.Count > 0)
            {
                return new SelectionCaptureResult(validPreselection, false);
            }

            try
            {
                IList<Reference> picks = selectionFilter == null
                    ? uidoc.Selection.PickObjects(ObjectType.Element, prompt)
                    : uidoc.Selection.PickObjects(ObjectType.Element, selectionFilter, prompt);

                var pickedIds = new List<ElementId>();
                if (picks != null)
                {
                    foreach (Reference pickedRef in picks)
                    {
                        if (pickedRef != null && pickedRef.ElementId != null)
                        {
                            pickedIds.Add(pickedRef.ElementId);
                        }
                    }
                }

                IList<ElementId> validPicks = FilterDistinctElementIds(uidoc.Document, pickedIds, selectionFilter, excluded);
                return new SelectionCaptureResult(validPicks, false);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return new SelectionCaptureResult(new List<ElementId>(), true);
            }
        }

        public static bool TryPickSingleElementId(
            UIDocument uidoc,
            ISelectionFilter selectionFilter,
            string prompt,
            out ElementId elementId,
            out bool wasCancelled)
        {
            elementId = ElementId.InvalidElementId;
            wasCancelled = false;

            if (uidoc == null || uidoc.Document == null)
            {
                return false;
            }

            try
            {
                Reference pickedRef = selectionFilter == null
                    ? uidoc.Selection.PickObject(ObjectType.Element, prompt)
                    : uidoc.Selection.PickObject(ObjectType.Element, selectionFilter, prompt);

                if (pickedRef == null || pickedRef.ElementId == null || pickedRef.ElementId == ElementId.InvalidElementId)
                {
                    return false;
                }

                if (uidoc.Document.GetElement(pickedRef.ElementId) == null)
                {
                    return false;
                }

                elementId = pickedRef.ElementId;
                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                wasCancelled = true;
                return false;
            }
        }

        private static ISet<int> BuildExcludedSet(ICollection<ElementId> excludedElementIds)
        {
            var set = new HashSet<int>();
            if (excludedElementIds == null)
            {
                return set;
            }

            foreach (ElementId id in excludedElementIds)
            {
                if (id != null && id != ElementId.InvalidElementId)
                {
                    set.Add(ElementIdHelper.GetIntegerValue(id));
                }
            }

            return set;
        }

        private static IList<ElementId> FilterDistinctElementIds(
            Document doc,
            IEnumerable<ElementId> elementIds,
            ISelectionFilter selectionFilter,
            ISet<int> excluded)
        {
            var result = new List<ElementId>();
            if (doc == null || elementIds == null)
            {
                return result;
            }

            var seen = new HashSet<int>();

            foreach (ElementId id in elementIds)
            {
                if (id == null || id == ElementId.InvalidElementId)
                {
                    continue;
                }

                int key = ElementIdHelper.GetIntegerValue(id);
                if (excluded != null && excluded.Contains(key))
                {
                    continue;
                }

                if (seen.Contains(key))
                {
                    continue;
                }

                Element element = doc.GetElement(id);
                if (element == null)
                {
                    continue;
                }

                if (selectionFilter != null && !selectionFilter.AllowElement(element))
                {
                    continue;
                }

                seen.Add(key);
                result.Add(id);
            }

            return result;
        }
    }
}
