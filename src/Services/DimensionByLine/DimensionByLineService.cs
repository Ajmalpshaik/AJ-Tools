// Tool Name: Dimension By Line Service
// Description: Creates dimensions for grids or levels along a picked line segment.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Services.DimensionByLine
{
    /// <summary>
    /// Service methods to create dimensions across grids or levels along a picked line.
    /// </summary>
    internal static class DimensionByLineService
    {
        /// <summary>
        /// Dimensions levels along a user-picked line in section/elevation.
        /// </summary>
        internal static Result DimensionLevels(ExternalCommandData data)
        {
            const string title = "Dim By Line - Level Only";

            try
            {
                UIApplication uiapp = data.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                if (uidoc == null)
                    return Fail(title, "Open a project view before running this command.");

                Document doc = uidoc.Document;
                View view = doc.ActiveView;
                if (view == null || view.IsTemplate)
                    return Fail(title, "Please run this tool in a normal project view.");

                if (view.ViewType != ViewType.Section && view.ViewType != ViewType.Elevation)
                    return Fail(title, "This tool only works in Section or Elevation views.");

                if (!PrepareSketchPlane(doc, view))
                    return Fail(title, "Could not prepare the view for selection.");

                if (!PickDimensionLinePoints(uidoc, out var p1, out var p2))
                    return Result.Cancelled;

                var levelsToDim = GetLevelsToDimension(doc, view, p1, p2);
                if (levelsToDim.Count < 2)
                    return Fail(title, "Fewer than two levels were found within the picked range.");

                using (Transaction t = new Transaction(doc, title))
                {
                    try
                    {
                        t.Start();
                        CreateLevelDimension(doc, view, levelsToDim, p1);
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        if (t.HasStarted() && !t.HasEnded())
                            t.RollBack();
                        return Fail(title, $"Failed to create level dimensions: {ex.Message}");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(title, "An error occurred:\n" + ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Dimensions grids along a user-picked line (plan + section/elevation supported).
        /// </summary>
        internal static Result DimensionGrids(ExternalCommandData data)
        {
            const string title = "Dim By Line - Grid Only";

            try
            {
                UIApplication uiapp = data.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                if (uidoc == null)
                    return Fail(title, "Open a project view before running this command.");

                Document doc = uidoc.Document;
                View view = doc.ActiveView;
                if (view == null || view.IsTemplate)
                    return Fail(title, "Please run this tool in a normal project view.");

                if (!PickSelectionLinePoints(uidoc, out var p1, out var p2))
                    return Result.Cancelled;

                var gridLines = GetGridLinesInView(doc, view);
                if (gridLines.Count < 2)
                    return Fail(title, "Need at least two visible grids in this view.");

                if (p1.DistanceTo(p2) < Constants.MIN_DISTANCE_TOLERANCE)
                    return Result.Cancelled;

                var gridsToDim = FilterGridLines(gridLines, view, p1, p2);
                if (gridsToDim.Count < 2)
                    return Fail(title, "Pick a line that crosses at least two visible grids in this view direction.");

                using (Transaction t = new Transaction(doc, title))
                {
                    try
                    {
                        t.Start();
                        CreateGridDimension(doc, view, gridsToDim, p1);
                        t.Commit();
                    }
                    catch (Exception ex)
                    {
                        if (t.HasStarted() && !t.HasEnded())
                            t.RollBack();
                        return Fail(title, $"Failed to create grid dimensions: {ex.Message}");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(title, "An error occurred:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static bool PrepareSketchPlane(Document doc, View view)
        {
            try
            {
                using (Transaction t = new Transaction(doc, "Set Sketch Plane"))
                {
                    t.Start();
                    Plane plane = Plane.CreateByOriginAndBasis(view.Origin, view.RightDirection, view.UpDirection);
                    SketchPlane sp = SketchPlane.Create(doc, plane);
                    view.SketchPlane = sp;
                    t.Commit();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool PickDimensionLinePoints(UIDocument uidoc, out XYZ p1, out XYZ p2)
        {
            p1 = null;
            p2 = null;

            try
            {
                p1 = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick the START point for the dimension line");
                p2 = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick the END point for the dimension line");

                return p1.DistanceTo(p2) >= Constants.MIN_DISTANCE_TOLERANCE;
            }
            catch
            {
                return false;
            }
        }

        private static List<Level> GetLevelsToDimension(Document doc, View view, XYZ p1, XYZ p2)
        {
            IList<Level> levels = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            double minZ = Math.Min(p1.Z, p2.Z);
            double maxZ = Math.Max(p1.Z, p2.Z);

            return levels
                .Where(l => l.Elevation >= minZ - Constants.ELEVATION_EPSILON && 
                           l.Elevation <= maxZ + Constants.ELEVATION_EPSILON)
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        private static void CreateLevelDimension(Document doc, View view, List<Level> levelsToDim, XYZ p1)
        {
            ReferenceArray refs = new ReferenceArray();
            foreach (Level lvl in levelsToDim)
            {
                refs.Append(new Reference(lvl));
            }

            XYZ dimStart = new XYZ(p1.X, p1.Y, levelsToDim.First().Elevation);
            XYZ dimEnd = new XYZ(p1.X, p1.Y, levelsToDim.Last().Elevation);
            Line dimLine = Line.CreateBound(dimStart, dimEnd);

            doc.Create.NewDimension(view, dimLine, refs);
        }

        private static bool PickSelectionLinePoints(UIDocument uidoc, out XYZ p1, out XYZ p2)
        {
            p1 = null;
            p2 = null;

            try
            {
                p1 = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick the START point of the selection line");
                p2 = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick the END point of the selection line");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static List<GridLineInfo> GetGridLinesInView(Document doc, View view)
        {
            IList<Grid> grids = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            var results = new List<GridLineInfo>();

            foreach (Grid grid in grids)
            {
                Line line = TryGetGridLineInView(grid, view);
                if (line == null)
                    continue;

                XYZ startView = ToViewCoordinates(view, line.GetEndPoint(0));
                XYZ endView = ToViewCoordinates(view, line.GetEndPoint(1));
                XYZ dirView = new XYZ(endView.X - startView.X, endView.Y - startView.Y, 0);
                if (dirView.IsZeroLength())
                    continue;

                results.Add(new GridLineInfo
                {
                    Grid = grid,
                    ViewLine = line,
                    StartView = startView,
                    EndView = endView,
                    DirectionView = dirView.Normalize()
                });
            }

            return results;
        }

        private static List<GridLineInfo> FilterGridLines(
            List<GridLineInfo> gridLines,
            View view,
            XYZ pick1,
            XYZ pick2)
        {
            var filtered = new List<GridLineInfo>();
            if (gridLines == null || gridLines.Count == 0)
                return filtered;

            XYZ p1 = ToViewCoordinates(view, pick1);
            XYZ p2 = ToViewCoordinates(view, pick2);
            XYZ selectionDir = new XYZ(p2.X - p1.X, p2.Y - p1.Y, 0);
            if (selectionDir.IsZeroLength())
                return filtered;

            selectionDir = selectionDir.Normalize();

            foreach (GridLineInfo info in gridLines)
            {
                info.AngleScore = Math.Abs(info.DirectionView.DotProduct(selectionDir));
            }

            double minScore = gridLines.Min(g => g.AngleScore);
            const double dirTolerance = 0.1; // allow ~6 degrees from the best match

            foreach (GridLineInfo info in gridLines)
            {
                if (info.AngleScore > minScore + dirTolerance)
                    continue;

                if (LinesIntersect2D(p1, p2, info.StartView, info.EndView))
                    filtered.Add(info);
            }

            return filtered;
        }

        private static void CreateGridDimension(
            Document doc,
            View view,
            List<GridLineInfo> gridsToDim,
            XYZ anchorPoint)
        {
            if (gridsToDim == null || gridsToDim.Count < 2)
                throw new InvalidOperationException("Need at least two grids to dimension.");

            XYZ viewNormal = view.ViewDirection.Normalize();
            XYZ gridDir = gridsToDim[0].ViewLine.Direction.Normalize();

            XYZ dimDir = gridDir.CrossProduct(viewNormal);
            if (dimDir.IsZeroLength())
                dimDir = view.RightDirection;

            dimDir = dimDir.Normalize();
            double anchorCoord = anchorPoint.DotProduct(dimDir);

            foreach (GridLineInfo info in gridsToDim)
            {
                XYZ mid = (info.ViewLine.GetEndPoint(0) + info.ViewLine.GetEndPoint(1)) * 0.5;
                info.SortCoord = mid.DotProduct(dimDir);
            }

            gridsToDim = gridsToDim
                .OrderBy(g => g.SortCoord)
                .ToList();

            ReferenceArray refs = new ReferenceArray();
            foreach (GridLineInfo info in gridsToDim)
                refs.Append(new Reference(info.Grid));

            double minCoord = gridsToDim.First().SortCoord;
            double maxCoord = gridsToDim.Last().SortCoord;
            double padding = 1.0; // 1 ft padding beyond the outermost grids

            XYZ dimStart = anchorPoint + dimDir * (minCoord - anchorCoord - padding);
            XYZ dimEnd = anchorPoint + dimDir * (maxCoord - anchorCoord + padding);

            doc.Create.NewDimension(view, Line.CreateBound(dimStart, dimEnd), refs);
        }

        private static Line TryGetGridLineInView(Grid grid, View view)
        {
            IList<Curve> curves = null;
            try
            {
                curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
            }
            catch
            {
                // ignore and fall back
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

            return curves?.OfType<Line>().FirstOrDefault();
        }

        private static XYZ ToViewCoordinates(View view, XYZ point)
        {
            XYZ origin = view.Origin;
            XYZ delta = point - origin;
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            XYZ normal = view.ViewDirection.Normalize();

            double x = delta.DotProduct(right);
            double y = delta.DotProduct(up);
            double z = delta.DotProduct(normal);

            return new XYZ(x, y, z);
        }

        private static bool LinesIntersect2D(XYZ a0, XYZ a1, XYZ b0, XYZ b1)
        {
            double ax = a1.X - a0.X;
            double ay = a1.Y - a0.Y;
            double bx = b1.X - b0.X;
            double by = b1.Y - b0.Y;

            double denom = ax * by - ay * bx;
            if (Math.Abs(denom) < Constants.ZERO_LENGTH_TOLERANCE)
                return false;

            double t = ((b0.X - a0.X) * by - (b0.Y - a0.Y) * bx) / denom;
            double u = ((b0.X - a0.X) * ay - (b0.Y - a0.Y) * ax) / denom;

            const double intersectTol = 0.05; // allow slightly outside the picked segment
            return t >= -intersectTol && t <= 1 + intersectTol &&
                   u >= -intersectTol && u <= 1 + intersectTol;
        }

        private class GridLineInfo
        {
            public Grid Grid { get; set; }
            public Line ViewLine { get; set; }
            public XYZ StartView { get; set; }
            public XYZ EndView { get; set; }
            public XYZ DirectionView { get; set; }
            public double SortCoord { get; set; }
            public double AngleScore { get; set; }
        }

        private static Result Fail(string title, string message)
        {
            DialogHelper.ShowError(title, message);
            return Result.Failed;
        }
    }
}
