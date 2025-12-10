// Tool Name: Auto Dimension Service
// Description: Core service to generate grid/level dimensions automatically along specified directions.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Linq
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;

namespace AJTools.Services
{
    internal enum AutoDimensionMode
    {
        Combined,
        GridsOnly,
        LevelsOnly
    }

    /// <summary>
    /// Provides routines to place automatic dimensions for grids and levels based on view context.
    /// </summary>
    internal static class AutoDimensionService
    {
        private const double MM_IN_FEET = 0.00328084;
        private const double PARALLEL_TOLERANCE = 0.001;

        private static readonly HashSet<ViewType> PlanViews = new HashSet<ViewType>
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.EngineeringPlan
        };

        private class GridEntry
        {
            public double Coord;
            public Grid Grid;
        }

        /// <summary>
        /// Entry point for auto-dimension commands; routes based on view type and mode.
        /// </summary>
        internal static Result Execute(
            ExternalCommandData commandData,
            AutoDimensionMode mode,
            string title)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null)
                {
                    TaskDialog.Show(title, "Open a project and active view before running this command.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                View view = doc.ActiveView;

                if (view == null || view.IsTemplate)
                {
                    TaskDialog.Show(title, "Please run this tool inside a normal project view.");
                    return Result.Failed;
                }

                if (!view.CropBoxActive)
                {
                    TaskDialog.Show(title, "Enable 'Crop View' for the active view and try again.");
                    return Result.Failed;
                }

                bool isPlan = PlanViews.Contains(view.ViewType) || IsAreaPlan(view.ViewType);
                bool isSection = view.ViewType == ViewType.Section || view.ViewType == ViewType.Elevation;

                switch (mode)
                {
                    case AutoDimensionMode.GridsOnly:
                        if (isPlan)
                            return CreatePlanGridDimensions(doc, view, title);
                        if (isSection)
                            return CreateSectionDimensions(doc, view, title, includeLevels: false, includeGrids: true);

                        TaskDialog.Show(title, "This tool works only in Plan, Section, or Elevation views.");
                        return Result.Cancelled;

                    case AutoDimensionMode.LevelsOnly:
                        if (!isSection)
                        {
                            TaskDialog.Show(title, "This tool only works in section or elevation views.");
                            return Result.Cancelled;
                        }
                        return CreateSectionDimensions(doc, view, title, includeLevels: true, includeGrids: false);

                    default:
                        if (isPlan)
                            return CreatePlanGridDimensions(doc, view, title);
                        if (isSection)
                            return CreateSectionDimensions(doc, view, title, includeLevels: true, includeGrids: true);

                        TaskDialog.Show(title, "This tool works only in Plan, Section, or Elevation views.");
                        return Result.Cancelled;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show(title, ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Defensive check for area plans without relying on enum value availability.
        /// </summary>
        private static bool IsAreaPlan(ViewType viewType)
        {
            try
            {
                return viewType.ToString() == "AreaPlan";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates dimensions across vertical/horizontal grids in plan views.
        /// </summary>
        private static Result CreatePlanGridDimensions(Document doc, View view, string title)
        {
            IList<Grid> grids = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            List<Grid> horizontalGrids = new List<Grid>();
            List<Grid> verticalGrids = new List<Grid>();

            foreach (Grid grid in grids)
            {
                Curve curve = grid.Curve;
                if (curve == null)
                    continue;

                XYZ direction = GetCurveDirection(curve);
                if (direction == null)
                    continue;

                if (Math.Abs(direction.Y) > 1.0 - PARALLEL_TOLERANCE)
                    verticalGrids.Add(grid);
                else if (Math.Abs(direction.X) > 1.0 - PARALLEL_TOLERANCE)
                    horizontalGrids.Add(grid);
            }

            if (horizontalGrids.Count < 2 && verticalGrids.Count < 2)
            {
                TaskDialog.Show(title, "Need at least two parallel grids visible in this plan view.");
                return Result.Cancelled;
            }

            BoundingBoxXYZ crop = view.CropBox;
            double scale = view.Scale;
            // Offset dimension strings based on view scale to avoid overlapping geometry.
            double offset = (8 * MM_IN_FEET) * scale;
            double overallOffset = (6 * MM_IN_FEET) * scale;

            int individualCount = 0;
            int overallCount = 0;

            using (Transaction t = new Transaction(doc, "AJ Tools - Auto Dimension Grids"))
            {
                t.Start();

                if (verticalGrids.Count >= 2)
                {
                    verticalGrids = verticalGrids
                        .OrderBy(g => g.Curve.GetEndPoint(0).X)
                        .ToList();

                    ReferenceArray allRefs = new ReferenceArray();
                    foreach (Grid grid in verticalGrids)
                        allRefs.Append(new Reference(grid));

                    XYZ p1 = new XYZ(crop.Min.X, crop.Max.Y + offset, 0);
                    XYZ p2 = new XYZ(crop.Max.X, crop.Max.Y + offset, 0);
                    doc.Create.NewDimension(view, Line.CreateBound(p1, p2), allRefs);
                    individualCount++;

                    ReferenceArray overallRefs = new ReferenceArray();
                    overallRefs.Append(new Reference(verticalGrids.First()));
                    overallRefs.Append(new Reference(verticalGrids.Last()));
                    XYZ p1o = new XYZ(p1.X, p1.Y + overallOffset, 0);
                    XYZ p2o = new XYZ(p2.X, p2.Y + overallOffset, 0);
                    doc.Create.NewDimension(view, Line.CreateBound(p1o, p2o), overallRefs);
                    overallCount++;
                }

                if (horizontalGrids.Count >= 2)
                {
                    horizontalGrids = horizontalGrids
                        .OrderBy(g => g.Curve.GetEndPoint(0).Y)
                        .ToList();

                    ReferenceArray allRefs = new ReferenceArray();
                    foreach (Grid grid in horizontalGrids)
                        allRefs.Append(new Reference(grid));

                    XYZ p1 = new XYZ(crop.Min.X - offset, crop.Min.Y, 0);
                    XYZ p2 = new XYZ(crop.Min.X - offset, crop.Max.Y, 0);
                    doc.Create.NewDimension(view, Line.CreateBound(p1, p2), allRefs);
                    individualCount++;

                    ReferenceArray overallRefs = new ReferenceArray();
                    overallRefs.Append(new Reference(horizontalGrids.First()));
                    overallRefs.Append(new Reference(horizontalGrids.Last()));
                    XYZ p1o = new XYZ(p1.X - overallOffset, p1.Y, 0);
                    XYZ p2o = new XYZ(p2.X - overallOffset, p2.Y, 0);
                    doc.Create.NewDimension(view, Line.CreateBound(p1o, p2o), overallRefs);
                    overallCount++;
                }

                t.Commit();
            }

            if (individualCount == 0)
            {
                TaskDialog.Show(title, "Could not create any dimensions. Ensure grids are visible and parallel.");
                return Result.Cancelled;
            }

            string summary = string.Format("Created {0} grid dimension string(s)", individualCount);
            if (overallCount > 0)
                summary += string.Format(" with {0} overall string(s)", overallCount);
            summary += ".";

            TaskDialog.Show(title, summary);
            return Result.Succeeded;
        }

        /// <summary>
        /// Creates dimensions for levels and/or grids in section/elevation views.
        /// </summary>
        private static Result CreateSectionDimensions(Document doc, View view, string title, bool includeLevels, bool includeGrids)
        {
            IList<Level> levels = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            IList<Grid> grids = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            BoundingBoxXYZ crop = view.CropBox;
            Transform transform = crop.Transform;
            Transform inverse = transform.Inverse;
            double scale = view.Scale;
            double offset = (8 * MM_IN_FEET) * scale;
            double levelOverallOffset = (10 * MM_IN_FEET) * scale;
            double gridOverallOffset = (10 * MM_IN_FEET) * scale;

            bool levelsDimmed = false;
            bool gridsDimmed = false;

            using (Transaction t = new Transaction(doc, "AJ Tools - Auto Dimension Levels/Grids"))
            {
                t.Start();

                if (includeLevels && levels.Count >= 2)
                {
                    List<Level> sortedLevels = levels
                        .OrderBy(l => l.Elevation)
                        .ToList();

                    double dimLineX = crop.Min.X - offset;
                    double dimLineY = (crop.Min.Y + crop.Max.Y) / 2.0;

                    ReferenceArray refAll = new ReferenceArray();
                    foreach (Level level in sortedLevels)
                        refAll.Append(new Reference(level));

                    XYZ p1 = new XYZ(dimLineX, dimLineY, sortedLevels.First().Elevation);
                    XYZ p2 = new XYZ(dimLineX, dimLineY, sortedLevels.Last().Elevation);
                    doc.Create.NewDimension(view, Line.CreateBound(p1, p2), refAll);

                    ReferenceArray refOverall = new ReferenceArray();
                    refOverall.Append(new Reference(sortedLevels.First()));
                    refOverall.Append(new Reference(sortedLevels.Last()));
                    double dimLineXOverall = dimLineX - levelOverallOffset;
                    XYZ p1o = new XYZ(dimLineXOverall, dimLineY, sortedLevels.First().Elevation);
                    XYZ p2o = new XYZ(dimLineXOverall, dimLineY, sortedLevels.Last().Elevation);
                    doc.Create.NewDimension(view, Line.CreateBound(p1o, p2o), refOverall);

                    levelsDimmed = true;
                }

                if (includeGrids)
                {
                    List<GridEntry> gridEntries = new List<GridEntry>();
                    foreach (Grid grid in grids)
                    {
                        IList<Curve> curves = null;
                        try
                        {
                            curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                        }
                        catch
                        {
                        }

                        if (curves == null || curves.Count == 0)
                        {
                            try
                            {
                                curves = grid.GetCurvesInView(DatumExtentType.Model, view);
                            }
                            catch
                            {
                                curves = null;
                            }
                        }

                        if (curves == null || curves.Count == 0)
                            continue;

                        Line line = curves.OfType<Line>().FirstOrDefault();
                        if (line == null)
                            continue;

                        XYZ local0 = inverse.OfPoint(line.GetEndPoint(0));
                        XYZ local1 = inverse.OfPoint(line.GetEndPoint(1));
                        double rightCoord = (local0.X + local1.X) / 2.0;
                        gridEntries.Add(new GridEntry { Coord = rightCoord, Grid = grid });
                    }

                    if (gridEntries.Count >= 2)
                    {
                        gridEntries.Sort((a, b) => a.Coord.CompareTo(b.Coord));

                        ReferenceArray refAllGrids = new ReferenceArray();
                        foreach (GridEntry entry in gridEntries)
                            refAllGrids.Append(new Reference(entry.Grid));

                        double viewDepth = crop.Min.Z;
                        double dimY = crop.Max.Y + offset;
                        double overallY = dimY + gridOverallOffset;

                        GridEntry firstEntry = gridEntries[0];
                        GridEntry lastEntry = gridEntries[gridEntries.Count - 1];

                        XYZ start = transform.OfPoint(new XYZ(firstEntry.Coord, dimY, viewDepth));
                        XYZ end = transform.OfPoint(new XYZ(lastEntry.Coord, dimY, viewDepth));
                        doc.Create.NewDimension(view, Line.CreateBound(start, end), refAllGrids);

                        ReferenceArray refOverall = new ReferenceArray();
                        refOverall.Append(new Reference(firstEntry.Grid));
                        refOverall.Append(new Reference(lastEntry.Grid));
                        XYZ startOverall = transform.OfPoint(new XYZ(firstEntry.Coord, overallY, viewDepth));
                        XYZ endOverall = transform.OfPoint(new XYZ(lastEntry.Coord, overallY, viewDepth));
                        doc.Create.NewDimension(view, Line.CreateBound(startOverall, endOverall), refOverall);

                        gridsDimmed = true;
                    }
                }

                t.Commit();
            }

            if (includeLevels && !levelsDimmed && !includeGrids)
            {
                TaskDialog.Show(title, "Need at least two levels visible in this view to create dimensions.");
                return Result.Cancelled;
            }

            if (includeGrids && !gridsDimmed && !includeLevels)
            {
                TaskDialog.Show(title, "Need at least two grids visible in this view direction to create dimensions.");
                return Result.Cancelled;
            }

            if (includeLevels && includeGrids)
            {
                if (!levelsDimmed && !gridsDimmed)
                {
                    TaskDialog.Show(title, "No suitable grids or levels were found to dimension.");
                    return Result.Cancelled;
                }

                string summary;
                if (levelsDimmed && gridsDimmed)
                    summary = "Created dimension strings for levels and grids.";
                else if (levelsDimmed)
                    summary = "Created level dimension strings.";
                else
                    summary = "Created grid dimension strings.";

                TaskDialog.Show(title, summary);
                return Result.Succeeded;
            }

            if (includeLevels && levelsDimmed)
            {
                TaskDialog.Show(title, "Created level dimension strings.");
                return Result.Succeeded;
            }

            if (includeGrids && gridsDimmed)
            {
                TaskDialog.Show(title, "Created grid dimension strings.");
                return Result.Succeeded;
            }

            TaskDialog.Show(title, "No dimension strings were created.");
            return Result.Cancelled;
        }

        private static XYZ GetCurveDirection(Curve curve)
        {
            try
            {
                return curve.ComputeDerivatives(0.5, true).BasisX.Normalize();
            }
            catch
            {
                Line line = curve as Line;
                if (line != null)
                    return line.Direction;
                return null;
            }
        }
    }
}
