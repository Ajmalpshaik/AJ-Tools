using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace AJTools
{
    internal static class DimensionByLineService
    {
        private const double MinPickDistance = 0.001;
        private const int DirectionPrecision = 3;

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

                // Set sketch plane to allow picking points reliably in the current view orientation.
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
                }
                catch (Exception ex)
                {
                    return Fail(title, "Could not prepare the view for selection:\n" + ex.Message);
                }

                XYZ p1;
                XYZ p2;
                try
                {
                    p1 = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick the START point for the dimension line");
                    p2 = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick the END point for the dimension line");
                }
                catch
                {
                    return Result.Cancelled;
                }

                if (p1.DistanceTo(p2) < MinPickDistance)
                    return Result.Cancelled;

                IList<Level> levels = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .ToList();

                if (levels.Count < 2)
                    return Fail(title, "Fewer than two levels were found in this view.");

                double minZ = Math.Min(p1.Z, p2.Z);
                double maxZ = Math.Max(p1.Z, p2.Z);

                List<Level> toDim = levels
                    .Where(l => l.Elevation >= minZ - 1e-6 && l.Elevation <= maxZ + 1e-6)
                    .OrderBy(l => l.Elevation)
                    .ToList();

                if (toDim.Count < 2)
                    return Fail(title, "Fewer than two levels were found within the picked range.");

                using (Transaction t = new Transaction(doc, title))
                {
                    t.Start();

                    ReferenceArray refs = new ReferenceArray();
                    foreach (Level lvl in toDim)
                        refs.Append(new Reference(lvl));

                    XYZ dimStart = new XYZ(p1.X, p1.Y, toDim.First().Elevation);
                    XYZ dimEnd = new XYZ(p1.X, p1.Y, toDim.Last().Elevation);
                    Line dimLine = Line.CreateBound(dimStart, dimEnd);

                    doc.Create.NewDimension(view, dimLine, refs);

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

                XYZ p1;
                XYZ p2;
                try
                {
                    p1 = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick the START point of the selection line");
                    p2 = uidoc.Selection.PickPoint(ObjectSnapTypes.None, "Pick the END point of the selection line");
                }
                catch
                {
                    return Result.Cancelled;
                }

                Line selectionLine = Line.CreateBound(p1, p2);
                if (selectionLine.Length < MinPickDistance)
                    return Result.Cancelled;

                // Flatten to XY for intersection tests.
                XYZ p1Flat = new XYZ(p1.X, p1.Y, 0);
                XYZ p2Flat = new XYZ(p2.X, p2.Y, 0);
                Line selectionFlat = Line.CreateBound(p1Flat, p2Flat);
                XYZ selectionDirFlat = (p2Flat - p1Flat).Normalize();

                IList<Grid> grids = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .ToList();

                // Group by direction signature.
                Dictionary<string, List<Grid>> groups = new Dictionary<string, List<Grid>>();
                foreach (Grid grid in grids)
                {
                    Curve c = grid.Curve;
                    if (c == null)
                        continue;

                    XYZ dir = GetCurveDirection(c);
                    if (dir == null)
                        continue;

                    string key = string.Format("{0:F" + DirectionPrecision + "},{1:F" + DirectionPrecision + "}", dir.X, dir.Y);
                    if (!groups.ContainsKey(key))
                        groups[key] = new List<Grid>();
                    groups[key].Add(grid);
                }

                List<Grid> bestGroup = null;
                double minDot = 1.0;

                foreach (List<Grid> group in groups.Values)
                {
                    if (group.Count < 2)
                        continue;

                    Grid sample = group[0];
                    XYZ dir = GetCurveDirection(sample.Curve);
                    if (dir == null)
                        continue;

                    double dot = Math.Abs(selectionDirFlat.DotProduct(new XYZ(dir.X, dir.Y, 0).Normalize()));
                    if (dot < minDot)
                    {
                        minDot = dot;
                        bestGroup = group;
                    }
                }

                List<Grid> gridsToDim = new List<Grid>();
                if (bestGroup != null)
                {
                    foreach (Grid grid in bestGroup)
                    {
                        Curve c = grid.Curve;
                        if (c == null)
                            continue;

                        Line testLine = null;
                        Line line = c as Line;
                        if (line != null)
                        {
                            XYZ gp1 = line.GetEndPoint(0);
                            XYZ gDir = line.Direction.Normalize();
                            testLine = Line.CreateUnbound(new XYZ(gp1.X, gp1.Y, 0), new XYZ(gDir.X, gDir.Y, 0));
                        }

                        if (testLine != null)
                        {
                            SetComparisonResult result = testLine.Intersect(selectionFlat);
                            if (result == SetComparisonResult.Overlap)
                                gridsToDim.Add(grid);
                        }
                    }
                }

                if (gridsToDim.Count < 2)
                    return Fail(title, "Fewer than two parallel grids were found intersecting your line.");

                gridsToDim = gridsToDim.OrderBy(g => g.Name).ToList();

                using (Transaction t = new Transaction(doc, title))
                {
                    t.Start();

                    ReferenceArray refs = new ReferenceArray();
                    foreach (Grid grid in gridsToDim)
                        refs.Append(new Reference(grid));

                    XYZ gridDir = GetCurveDirection(gridsToDim[0].Curve);
                    if (gridDir == null)
                        return Fail(title, "Cannot determine grid direction.");
                    XYZ dimDir = gridDir.CrossProduct(XYZ.BasisZ);
                    if (dimDir.IsZeroLength())
                        return Fail(title, "Cannot determine a perpendicular direction for the dimension line.");

                    dimDir = dimDir.Normalize();
                    XYZ dimStart = p1;
                    XYZ dimEnd = p1 + dimDir * 10.0; // nominal length; Revit will extend as needed
                    Line dimLine = Line.CreateBound(dimStart, dimEnd);

                    doc.Create.NewDimension(view, dimLine, refs);

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

            if (c is Line line)
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
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return DimensionByLineService.DimensionLevels(commandData);
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdDimensionGridsByLine : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return DimensionByLineService.DimensionGrids(commandData);
        }
    }
}
