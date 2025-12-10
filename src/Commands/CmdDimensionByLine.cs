// Tool Name: Dimension By Line
// Description: Creates dimensions for grids or levels along a picked line segment.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AJTools.Commands
{
    /// <summary>
    /// Service methods to create dimensions across grids or levels along a picked line.
    /// </summary>
    internal static class DimensionByLineService
    {
        private const double MinPickDistance = 0.001;
        private const int DirectionPrecision = 3;

        /// <summary>
        /// Dimensions levels along a user-picked line in section/elevation.
        /// </summary>
        internal static Result DimensionLevels(ExternalCommandData data)
        {
            const string title = "Dimension Levels by Line";
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
                    t.Start();
                    CreateLevelDimension(doc, view, levelsToDim, p1);
                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(title, "An error occurred:\n" + ex.Message);
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
                return p1.DistanceTo(p2) >= MinPickDistance;
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
                .Where(l => l.Elevation >= minZ - 1e-6 && l.Elevation <= maxZ + 1e-6)
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        private static void CreateLevelDimension(Document doc, View view, List<Level> levelsToDim, XYZ p1)
        {
            ReferenceArray refs = new ReferenceArray();
            foreach (Level lvl in levelsToDim)
                refs.Append(new Reference(lvl));

            XYZ dimStart = new XYZ(p1.X, p1.Y, levelsToDim.First().Elevation);
            XYZ dimEnd = new XYZ(p1.X, p1.Y, levelsToDim.Last().Elevation);
            Line dimLine = Line.CreateBound(dimStart, dimEnd);

            doc.Create.NewDimension(view, dimLine, refs);
        }

        /// <summary>
        /// Dimensions grids along a user-picked line in plan.
        /// </summary>
        internal static Result DimensionGrids(ExternalCommandData data)
        {
            const string title = "Dimension Grids by Line";
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

                var selectionLine = Line.CreateBound(p1, p2);
                if (selectionLine.Length < MinPickDistance)
                    return Result.Cancelled;

                var gridGroups = GroupGridsByDirection(doc, view);
                var bestGroup = FindBestGridGroup(gridGroups, selectionLine);
                var gridsToDim = GetGridsToDimension(bestGroup, selectionLine);

                if (gridsToDim.Count < 2)
                    return Fail(title, "Fewer than two parallel grids were found intersecting your line.");

                using (Transaction t = new Transaction(doc, title))
                {
                    t.Start();
                    CreateGridDimension(doc, view, gridsToDim, p1);
                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(title, "An error occurred:\n" + ex.Message);
                return Result.Failed;
            }
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

        private static Dictionary<string, List<Grid>> GroupGridsByDirection(Document doc, View view)
        {
            IList<Grid> grids = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            var groups = new Dictionary<string, List<Grid>>();
            foreach (Grid grid in grids)
            {
                Curve c = grid.Curve;
                if (c == null) continue;

                XYZ dir = GetCurveDirection(c);
                if (dir == null) continue;

                string key = string.Format("{0:F" + DirectionPrecision + "},{1:F" + DirectionPrecision + "}", dir.X, dir.Y);
                if (!groups.ContainsKey(key))
                    groups[key] = new List<Grid>();
                groups[key].Add(grid);
            }
            return groups;
        }

        private static List<Grid> FindBestGridGroup(Dictionary<string, List<Grid>> groups, Line selectionLine)
        {
            XYZ p1Flat = new XYZ(selectionLine.GetEndPoint(0).X, selectionLine.GetEndPoint(0).Y, 0);
            XYZ p2Flat = new XYZ(selectionLine.GetEndPoint(1).X, selectionLine.GetEndPoint(1).Y, 0);
            XYZ selectionDirFlat = (p2Flat - p1Flat).Normalize();

            List<Grid> bestGroup = null;
            double minDot = 1.0;

            foreach (List<Grid> group in groups.Values)
            {
                if (group.Count < 2) continue;

                Grid sample = group[0];
                XYZ dir = GetCurveDirection(sample.Curve);
                if (dir == null) continue;

                double dot = Math.Abs(selectionDirFlat.DotProduct(new XYZ(dir.X, dir.Y, 0).Normalize()));
                if (dot < minDot)
                {
                    minDot = dot;
                    bestGroup = group;
                }
            }
            return bestGroup;
        }

        private static List<Grid> GetGridsToDimension(List<Grid> gridGroup, Line selectionLine)
        {
            var gridsToDim = new List<Grid>();
            if (gridGroup == null) return gridsToDim;

            XYZ p1Flat = new XYZ(selectionLine.GetEndPoint(0).X, selectionLine.GetEndPoint(0).Y, 0);
            XYZ p2Flat = new XYZ(selectionLine.GetEndPoint(1).X, selectionLine.GetEndPoint(1).Y, 0);
            Line selectionFlat = Line.CreateBound(p1Flat, p2Flat);

            foreach (Grid grid in gridGroup)
            {
                Curve c = grid.Curve;
                if (c == null) continue;

                if (c is Line line)
                {
                    XYZ gp1 = line.GetEndPoint(0);
                    XYZ gDir = line.Direction.Normalize();
                    var testLine = Line.CreateUnbound(new XYZ(gp1.X, gp1.Y, 0), new XYZ(gDir.X, gDir.Y, 0));
                    if (testLine.Intersect(selectionFlat) == SetComparisonResult.Overlap)
                    {
                        gridsToDim.Add(grid);
                    }
                }
            }
            return gridsToDim.OrderBy(g => g.Name).ToList();
        }

        private static void CreateGridDimension(Document doc, View view, List<Grid> gridsToDim, XYZ p1)
        {
            ReferenceArray refs = new ReferenceArray();
            foreach (Grid grid in gridsToDim)
                refs.Append(new Reference(grid));

            XYZ gridDir = GetCurveDirection(gridsToDim[0].Curve);
            if (gridDir == null) throw new InvalidOperationException("Cannot determine grid direction.");

            XYZ dimDir = gridDir.CrossProduct(XYZ.BasisZ);
            if (dimDir.IsZeroLength()) throw new InvalidOperationException("Cannot determine a perpendicular direction for the dimension line.");

            dimDir = dimDir.Normalize();
            XYZ dimStart = p1;
            XYZ dimEnd = p1 + dimDir * 10.0;
            Line dimLine = Line.CreateBound(dimStart, dimEnd);

            doc.Create.NewDimension(view, dimLine, refs);
        }

        private static Result Fail(string title, string message)
        {
            TaskDialog.Show(title, message);
            return Result.Failed;
        }

        private static bool IsZeroLength(this XYZ vector)
        {
            return vector == null || vector.GetLength() < 1e-9;
        }

        private static XYZ GetCurveDirection(Curve c)
        {
            if (c == null)
                return null;

            Line line = c as Line;
            if (line != null)
            {
                var d = line.Direction;
                return d.IsZeroLength() ? null : d.Normalize();
            }

            if (c.IsBound)
            {
                XYZ p0 = c.GetEndPoint(0);
                XYZ p1 = c.GetEndPoint(1);
                XYZ v = p1 - p0;
                if (v.IsZeroLength())
                    return null;
                return v.Normalize();
            }

            return null;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdDimensionLevelsByLine : IExternalCommand
    {
        /// <summary>
        /// Entry point for level-by-line dimensioning.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return DimensionByLineService.DimensionLevels(commandData);
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdDimensionGridsByLine : IExternalCommand
    {
        /// <summary>
        /// Entry point for grid-by-line dimensioning.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return DimensionByLineService.DimensionGrids(commandData);
        }
    }
}
