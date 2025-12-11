using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Centers selected text notes or labels in the active family view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdResetTextPosition : IExternalCommand
    {
        /// <summary>
        /// Centers supported text elements relative to the active family view extents.
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

            if (!doc.IsFamilyDocument)
            {
                TaskDialog.Show("Center Text/Label", "Run this tool inside a family document.");
                return Result.Cancelled;
            }

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TaskDialog.Show("Center Text/Label", "Select one or more text notes or labels in the family view, then run the command.");
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

                    if (CenterElement(element, view, viewCenter))
                    {
                        centeredCount++;
                    }
                }

                transaction.Commit();
            }

            if (centeredCount == 0)
            {
                TaskDialog.Show("Center Text/Label", "The selection did not contain movable text notes or labels.");
                return Result.Cancelled;
            }

            TaskDialog.Show("Center Text/Label", $"Centered {centeredCount} item(s) in the active family view.");
            return Result.Succeeded;
        }

        private static bool CenterElement(Element element, View view, XYZ viewCenter)
        {
            if (element is IndependentTag tag)
                return CenterTag(tag, view, viewCenter);

            if (element.Location is LocationPoint locationPoint)
                return CenterLocationPoint(locationPoint, view, viewCenter);

            return false;
        }

        private static bool CenterTag(IndependentTag tag, View view, XYZ viewCenter)
        {
            if (tag == null)
                return false;

            XYZ target = AlignToViewPlane(view, tag.TagHeadPosition, viewCenter);
            if (target == null || tag.TagHeadPosition.IsAlmostEqualTo(target))
                return false;

            tag.TagHeadPosition = target;
            return true;
        }

        private static bool CenterLocationPoint(LocationPoint locationPoint, View view, XYZ viewCenter)
        {
            if (locationPoint == null)
                return false;

            XYZ target = AlignToViewPlane(view, locationPoint.Point, viewCenter);
            if (target == null || locationPoint.Point.IsAlmostEqualTo(target))
                return false;

            locationPoint.Point = target;
            return true;
        }

        private static XYZ AlignToViewPlane(View view, XYZ current, XYZ viewCenter)
        {
            if (view == null || current == null || viewCenter == null)
                return null;

            XYZ viewDirection = view.ViewDirection;
            if (viewDirection == null || viewDirection.GetLength() < 1e-9)
                return viewCenter;

            XYZ normal = viewDirection.Normalize();
            double depth = normal.DotProduct(current.Subtract(viewCenter));

            return viewCenter.Add(normal.Multiply(depth));
        }

        private static XYZ ResolveViewCenter(View view)
        {
            if (view == null)
                return null;

            BoundingBoxXYZ box = null;

            if (view is View3D view3D && view3D.IsSectionBoxActive)
            {
                box = view3D.GetSectionBox();
            }

            if (box == null)
            {
                try
                {
                    box = view.CropBox;
                }
                catch (InvalidOperationException)
                {
                    // Crop boxes are not available for some view types.
                }
            }

            if (box == null)
            {
                box = view.get_BoundingBox(null);
            }

            if (box == null)
                return null;

            XYZ localCenter = box.Min.Add(box.Max).Multiply(0.5);
            Transform transform = box.Transform ?? Transform.Identity;

            return transform.OfPoint(localCenter);
        }

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

            // Annotation/tag families expose a single non-template 2D view.
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
    }
}
