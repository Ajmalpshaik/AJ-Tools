// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Captures Apply Graphics selections and shared command execution context.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.2
// Created      : 2026-03-30
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit UI document and user selections.
// Output       : Validated command context and selected element ids.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.2 - Supports the unified Apply Graphics selection workflow.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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

            context = new GraphicsCommandContext(uidoc, doc, doc.ActiveView);
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
        public static IList<ElementId> GetValidPreselectedElementIds(
            UIDocument uidoc,
            ISelectionFilter selectionFilter,
            ICollection<ElementId> excludedElementIds = null)
        {
            if (uidoc == null || uidoc.Document == null)
            {
                return new List<ElementId>();
            }

            ICollection<ElementId> preselected = uidoc.Selection.GetElementIds();
            return FilterDistinctElementIds(
                uidoc.Document,
                preselected,
                selectionFilter,
                BuildExcludedSet(excludedElementIds));
        }

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
                    set.Add(id.IntegerValue);
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

                int key = id.IntegerValue;
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
