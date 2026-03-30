using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AJTools.Services.GraphicsTools
{
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

            ICollection<ElementId> preselected = uidoc.Selection.GetElementIds();
            IList<ElementId> validPreselection = FilterDistinctElementIds(uidoc.Document, preselected, selectionFilter, excluded);
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
