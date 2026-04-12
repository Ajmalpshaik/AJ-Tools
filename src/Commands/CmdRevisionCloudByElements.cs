using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Models.RevisionCloud;
using AJTools.Services.RevisionCloud;

namespace AJTools.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdRevisionCloudByElements : IExternalCommand
    {
        private const string ToolDisplayName = "Revision Cloud By Elements";
        private const string TransactionName = "AJ Tools - Revision Cloud By Elements";
        private const double DefaultCellSizeFeet = 10.0 * 0.00328084; // 10 mm

        private static readonly HashSet<ViewType> SupportedViewTypes = new HashSet<ViewType>
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.Section,
            ViewType.Elevation,
            ViewType.DraftingView,
            ViewType.Detail
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show(ToolDisplayName, "No active document.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                View activeView = doc.ActiveView;

                if (!SupportedViewTypes.Contains(activeView.ViewType))
                {
                    TaskDialog.Show(
                        ToolDisplayName,
                        "This tool supports Floor Plan, Ceiling Plan, Section, Elevation, Detail, and Drafting views only.");
                    return Result.Failed;
                }

                Revision latestRevision = GetLatestRevision(doc);
                if (latestRevision == null)
                {
                    TaskDialog.Show(
                        ToolDisplayName,
                        "No revisions found in the project. Please create a revision first.");
                    return Result.Failed;
                }

                var settings = RevisionCloudSettings.Load() ?? new RevisionCloudSettings();
                SanitizeSettings(settings);

                var viewPlane = GeometryProjectionService.GetViewPlane(activeView);
                if (viewPlane == null)
                {
                    TaskDialog.Show(ToolDisplayName, "Cannot determine view plane.");
                    return Result.Failed;
                }

                int passes = 0;
                int totalElementsProcessed = 0;
                int totalCloudsCreated = 0;
                int totalSkipped = 0;
                int totalFailures = 0;

                // Pass 1: use current selection if available; otherwise prompt.
                var selectedIds = GetSelectedElements(uidoc, true);
                if (selectedIds == null || selectedIds.Count == 0)
                    return Result.Cancelled;

                while (selectedIds != null && selectedIds.Count > 0)
                {
                    passes++;

                    CreateCloudsForSelection(
                        doc,
                        activeView,
                        viewPlane,
                        latestRevision.Id,
                        settings,
                        selectedIds,
                        out int elementsProcessed,
                        out int skippedCount,
                        out int cloudsCreated,
                        out int failedClouds);

                    totalElementsProcessed += elementsProcessed;
                    totalCloudsCreated += cloudsCreated;
                    totalSkipped += skippedCount;
                    totalFailures += failedClouds;

                    // Next pass: always prompt selection; press ESC to finish continuous mode.
                    selectedIds = GetSelectedElements(uidoc, false);
                }

                if (totalCloudsCreated > 0)
                {
                    TaskDialog.Show(
                        ToolDisplayName,
                        $"Selection passes: {passes}\n" +
                        $"Elements processed: {totalElementsProcessed}\n" +
                        $"Clouds created: {totalCloudsCreated}" +
                        (totalFailures > 0 ? $"\nCloud creation failures: {totalFailures}" : "") +
                        (totalSkipped > 0 ? $"\nSkipped (no geometry): {totalSkipped}" : ""));
                    return Result.Succeeded;
                }

                TaskDialog.Show(
                    ToolDisplayName,
                    "No revision clouds could be created. The geometry may be too small, not visible in this view, or invalid for cloud creation.");
                return Result.Failed;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show($"{ToolDisplayName} - Error", ex.Message);
                return Result.Failed;
            }
        }

        private void CreateCloudsForSelection(
            Document doc,
            View activeView,
            ViewPlaneData viewPlane,
            ElementId revisionId,
            RevisionCloudSettings settings,
            IList<ElementId> selectedIds,
            out int elementsProcessed,
            out int skippedCount,
            out int cloudsCreated,
            out int failedClouds)
        {
            elementsProcessed = 0;
            skippedCount = 0;
            cloudsCreated = 0;
            failedClouds = 0;

            var elementRects = new List<UVRect>();
            var orientationAngles = new List<double>();

            foreach (var id in selectedIds)
            {
                Element elem = doc.GetElement(id);
                if (elem == null)
                    continue;

                if (GeometryProjectionService.TryGetElementProjectedAxisAngle(elem, viewPlane, out double axisAngle))
                    orientationAngles.Add(axisAngle);
            }

            double dominantAxisAngle = GetDominantAxisAngle(orientationAngles);

            foreach (var id in selectedIds)
            {
                Element elem = doc.GetElement(id);
                if (elem == null)
                {
                    skippedCount++;
                    continue;
                }

                // Build each element rectangle directly in dominant-axis frame for stable
                // offset and better performance on angled selections.
                UVRect rect = GeometryProjectionService.GetProjectedBoundingBox(
                    elem,
                    activeView,
                    dominantAxisAngle);
                if (rect != null)
                {
                    elementRects.Add(rect);
                    elementsProcessed++;
                }
                else
                {
                    skippedCount++;
                }
            }

            if (elementRects.Count == 0)
                return;

            var polygons = OrthogonalOutlineBuilder.BuildOutlines(
                elementRects,
                settings.OffsetDistanceFeet,
                DefaultCellSizeFeet);
            if (polygons == null || polygons.Count == 0)
                return;

            using (Transaction t = new Transaction(doc, TransactionName))
            {
                t.Start();

                foreach (var polygon in polygons)
                {
                    if (polygon == null || polygon.Count < 4)
                    {
                        failedClouds++;
                        continue;
                    }

                    try
                    {
                        var polygonInViewFrame = RotatePolygonIfNeeded(polygon, dominantAxisAngle);
                        var cloud = RevisionCloudCreator.Create(doc, activeView, viewPlane, polygonInViewFrame, revisionId);
                        if (cloud != null)
                            cloudsCreated++;
                        else
                            failedClouds++;
                    }
                    catch
                    {
                        failedClouds++;
                    }
                }

                if (cloudsCreated > 0)
                    t.Commit();
                else
                    t.RollBack();
            }
        }

        private static void SanitizeSettings(RevisionCloudSettings settings)
        {
            if (settings == null)
                return;

            if (double.IsNaN(settings.OffsetDistanceMm) || double.IsInfinity(settings.OffsetDistanceMm) || settings.OffsetDistanceMm < 0)
                settings.OffsetDistanceMm = 50.0;
        }

        private static List<UV> RotatePolygonIfNeeded(List<UV> polygon, double angle)
        {
            if (polygon == null || polygon.Count == 0 || Math.Abs(angle) < 1e-9)
                return polygon;

            double cosA = Math.Cos(angle);
            double sinA = Math.Sin(angle);
            var rotated = new List<UV>(polygon.Count);
            foreach (var p in polygon)
            {
                rotated.Add(new UV(
                    p.U * cosA - p.V * sinA,
                    p.U * sinA + p.V * cosA));
            }

            return rotated;
        }

        /// <summary>
        /// Computes dominant axis angle from element angles, treating a and a+PI as the same axis.
        /// Uses doubled-angle averaging for stable orientation extraction.
        /// </summary>
        private static double GetDominantAxisAngle(IList<double> angles)
        {
            if (angles == null || angles.Count == 0)
                return 0.0;

            double x = 0.0;
            double y = 0.0;
            foreach (double a in angles)
            {
                double da = 2.0 * a;
                x += Math.Cos(da);
                y += Math.Sin(da);
            }

            if (Math.Abs(x) < 1e-12 && Math.Abs(y) < 1e-12)
                return 0.0;

            return 0.5 * Math.Atan2(y, x);
        }

        private List<ElementId> GetSelectedElements(UIDocument uidoc, bool allowPreSelection)
        {
            if (allowPreSelection)
            {
                var preSelected = uidoc.Selection.GetElementIds();
                if (preSelected != null && preSelected.Count > 0)
                {
                    var validIds = FilterValidElements(uidoc.Document, preSelected);
                    if (validIds.Count > 0)
                        return validIds;
                }
            }

            try
            {
                var refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new ModelElementSelectionFilter(),
                    "Select elements for revision cloud and press Finish. Press Esc to stop.");

                if (refs == null || refs.Count == 0)
                    return null;

                var ids = new List<ElementId>();
                foreach (var r in refs)
                    ids.Add(r.ElementId);

                return FilterValidElements(uidoc.Document, ids);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        private List<ElementId> FilterValidElements(Document doc, ICollection<ElementId> ids)
        {
            var result = new List<ElementId>();
            foreach (var id in ids)
            {
                Element elem = doc.GetElement(id);
                if (elem == null) continue;
                if (elem is ElementType) continue;
                if (elem is View) continue;
                if (elem is Autodesk.Revit.DB.RevisionCloud) continue;
                if (elem is RevitLinkInstance) continue;
                result.Add(id);
            }
            return result;
        }

        private Revision GetLatestRevision(Document doc)
        {
            Revision latest = null;
            var collector = new FilteredElementCollector(doc).OfClass(typeof(Revision));

            foreach (var elem in collector)
            {
                if (elem is Revision rev)
                {
                    if (latest == null || rev.SequenceNumber > latest.SequenceNumber)
                        latest = rev;
                }
            }

            return latest;
        }
    }

    internal class ModelElementSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem == null) return false;
            if (elem is ElementType) return false;
            if (elem is View) return false;
            if (elem is Autodesk.Revit.DB.RevisionCloud) return false;
            if (elem is RevitLinkInstance) return false;
            return true;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
