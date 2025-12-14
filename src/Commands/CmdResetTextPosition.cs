// Tool Name: Reset Text Position
// Description: Centers selected annotations in the active family view.
// Author: Ajmal P.S.
// Version: 1.0.3
// Last Updated: 2025-12-14
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Centers selected annotations (text, labels, symbols, tag graphics) in the active annotation family view.
    /// Works only inside editable annotation families.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetTextPosition : IExternalCommand
    {
        /// <summary>
        /// Centers supported elements relative to the active family view extents.
        /// </summary>
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "No active Revit document.";
                return Result.Failed;
            }

            // This tool is meant only for annotation / tag families.
            if (!doc.IsFamilyDocument)
            {
                DialogHelper.ShowError(
                    "Center Text/Label",
                    "Run this tool inside an annotation family.");
                return Result.Cancelled;
            }

            if (doc.IsReadOnly)
            {
                DialogHelper.ShowError(
                    "Center Text/Label",
                    "The family is read-only. Open an editable copy and try again.");
                return Result.Cancelled;
            }

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                DialogHelper.ShowError(
                    "Center Text/Label",
                    "Select one or more movable annotations (text notes, labels, symbols, or tag graphics) in the family view, then run the command.");
                return Result.Cancelled;
            }

            View view = ResolveFamilyView(uidoc);
            if (view == null)
            {
                message = "No valid family view is available. Open the family view and try again.";
                return Result.Failed;
            }

            XYZ viewCenter = ResolveViewCenter(view);
            if (viewCenter == null)
            {
                message = "Could not resolve the active family view extents.";
                return Result.Failed;
            }

            int centeredCount = 0;

            using (Transaction transaction = new Transaction(doc, "Center Text/Label"))
            {
                transaction.Start();

                foreach (ElementId id in selectedIds)
                {
                    Element element = doc.GetElement(id);
                    if (element == null)
                        continue;

                    if (CenterElement(doc, element, view, viewCenter))
                    {
                        centeredCount++;
                    }
                }

                transaction.Commit();
            }

            if (centeredCount == 0)
            {
                DialogHelper.ShowError(
                    "Center Text/Label",
                    "The selection did not contain elements that can be centered in this family view.");
                return Result.Cancelled;
            }

            DialogHelper.ShowInfo(
                "Center Text/Label",
                $"Centered {centeredCount} item(s) in the active family view.");

            return Result.Succeeded;
        }

        /// <summary>
        /// Moves an element so its center aligns with the given view center.
        /// Handles elements with a location point or any element with a bounding box.
        /// Returns true when the element is movable (even if already centered) so
        /// the caller can report success instead of an empty selection.
        /// </summary>
        private static bool CenterElement(Document doc, Element element, View view, XYZ viewCenter)
        {
            if (doc == null || element == null || viewCenter == null)
                return false;

            if (element.Pinned)
                return false;

            XYZ currentCenter = TryGetElementCenter(element, view);
            if (currentCenter == null)
                return false;

            // Keep the existing Z to respect the family work plane.
            XYZ move = new XYZ(
                viewCenter.X - currentCenter.X,
                viewCenter.Y - currentCenter.Y,
                0);

            // Already centered - count as handled so the user is not shown an error dialog.
            if (move.GetLength() < 1e-6)
                return true;

            try
            {
                ElementTransformUtils.MoveElement(doc, element.Id, move);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return false;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the geometric center of an element. Prefers its LocationPoint;
        /// falls back to the bounding box center in view coordinates.
        /// </summary>
        private static XYZ TryGetElementCenter(Element element, View view)
        {
            if (element?.Location is LocationPoint locationPoint && locationPoint.Point != null)
            {
                return locationPoint.Point;
            }

            BoundingBoxXYZ box = null;

            try
            {
                // Use a view-aware bounding box when possible for annotations.
                if (view != null)
                {
                    box = element.get_BoundingBox(view);
                }
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                // Some elements do not support a view-specific bounding box.
            }

            if (box == null)
            {
                box = element.get_BoundingBox(null);
            }

            if (box == null || box.Min == null || box.Max == null)
                return null;

            XYZ localCenter = box.Min.Add(box.Max).Multiply(0.5);
            Transform transform = box.Transform ?? Transform.Identity;

            return transform.OfPoint(localCenter);
        }

        /// <summary>
        /// Resolves the active family view. For annotation/tag families this is usually
        /// the single working view where the label/text is visible.
        /// </summary>
        private static View ResolveFamilyView(UIDocument uidoc)
        {
            if (uidoc == null)
                return null;

            Document doc = uidoc.Document;
            if (doc == null)
                return null;

            View view = uidoc.ActiveView ?? doc.ActiveView;
            if (view != null)
                return view;

            // Fallback: pick the first non-template 2D view.
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v =>
                    !v.IsTemplate &&
                    v.ViewType != ViewType.ProjectBrowser &&
                    v.ViewType != ViewType.Undefined &&
                    v.ViewType != ViewType.Schedule)
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds the geometric center of the view extents (crop box or bounding box).
        /// This gives us a "view centre" point in model coordinates.
        /// </summary>
        private static XYZ ResolveViewCenter(View view)
        {
            if (view == null)
                return null;

            BoundingBoxXYZ box = null;

            try
            {
                // Annotation family views normally have a crop box.
                box = view.CropBox;
            }
            catch (InvalidOperationException)
            {
                // Some view types may not expose a crop box.
            }

            if (box == null)
            {
                box = view.get_BoundingBox(null);
            }

            if (box == null || box.Min == null || box.Max == null)
                return null;

            XYZ localCenter = box.Min.Add(box.Max).Multiply(0.5);
            Transform transform = box.Transform ?? Transform.Identity;

            return transform.OfPoint(localCenter);
        }
    }
}
