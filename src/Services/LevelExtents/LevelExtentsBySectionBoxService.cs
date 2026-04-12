// Tool Name: Level Extents By Section Box Service
// Description: Maximizes all visible level 3D extents to fit the active 3D view's section box.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-12
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Services.LevelExtents
{
    /// <summary>
    /// Sets all visible level 3D extents to fit the active 3D view's section box.
    /// </summary>
    internal static class LevelExtentsBySectionBoxService
    {
        internal static Result Execute(ExternalCommandData commandData, string title)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                    return Fail(title, "Open a project view before running this command.");

                Document doc = uidoc.Document;
                View3D view3D = doc.ActiveView as View3D;
                if (view3D == null || view3D.IsTemplate)
                    return Fail(title, "Run this tool from an active 3D view.");

                if (!view3D.IsSectionBoxActive)
                    return Fail(title, "The active 3D view does not have its section box enabled.");

                BoundingBoxXYZ box = view3D.GetSectionBox();
                if (box == null)
                    return Fail(title, "Could not read the section box of the active 3D view.");

                // Section box is in its own transform; convert corners to world space.
                Transform t = box.Transform;
                XYZ[] worldCorners = new XYZ[]
                {
                    t.OfPoint(new XYZ(box.Min.X, box.Min.Y, box.Min.Z)),
                    t.OfPoint(new XYZ(box.Max.X, box.Min.Y, box.Min.Z)),
                    t.OfPoint(new XYZ(box.Max.X, box.Max.Y, box.Min.Z)),
                    t.OfPoint(new XYZ(box.Min.X, box.Max.Y, box.Min.Z)),
                    t.OfPoint(new XYZ(box.Min.X, box.Min.Y, box.Max.Z)),
                    t.OfPoint(new XYZ(box.Max.X, box.Min.Y, box.Max.Z)),
                    t.OfPoint(new XYZ(box.Max.X, box.Max.Y, box.Max.Z)),
                    t.OfPoint(new XYZ(box.Min.X, box.Max.Y, box.Max.Z)),
                };

                double minX = worldCorners.Min(p => p.X);
                double maxX = worldCorners.Max(p => p.X);
                double minY = worldCorners.Min(p => p.Y);
                double maxY = worldCorners.Max(p => p.Y);

                if ((maxX - minX) <= Constants.MIN_DISTANCE_TOLERANCE ||
                    (maxY - minY) <= Constants.MIN_DISTANCE_TOLERANCE)
                    return Fail(title, "Section box has invalid X/Y extents.");

                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .ToList();

                if (levels.Count == 0)
                    return Fail(title, "No levels found in the project.");

                List<View> datumViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate &&
                                (v.ViewType == ViewType.Elevation ||
                                 v.ViewType == ViewType.Section ||
                                 v.ViewType == ViewType.ThreeD))
                    .ToList();

                if (datumViews.Count == 0)
                    return Fail(title, "No elevation, section, or 3D views found to write level extents.");

                int updatedCount = 0;
                using (Transaction tx = new Transaction(doc, "AJ Tools - Maximize Levels by Section Box"))
                {
                    tx.Start();
                    foreach (Level level in levels)
                    {
                        if (ApplyBoxToLevel(level, datumViews, minX, maxX, minY, maxY))
                            updatedCount++;
                    }
                    tx.Commit();
                }

                DialogHelper.ShowInfo(title, $"Updated {updatedCount} level(s) to the active 3D view's section box.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(title, ex.Message);
                return Result.Failed;
            }
        }

        private static bool ApplyBoxToLevel(Level level, List<View> datumViews, double minX, double maxX, double minY, double maxY)
        {
            bool anyApplied = false;
            double z = level.Elevation;

            foreach (View view in datumViews)
            {
                Curve existing;
                if (!TryGetCurve(level, view, DatumExtentType.Model, out existing))
                    continue;

                if (!(existing is Line line))
                    continue;

                XYZ dir = line.Direction;
                XYZ p0, p1;

                if (Math.Abs(dir.X) >= Math.Abs(dir.Y))
                {
                    // level line runs primarily along X
                    p0 = new XYZ(minX, line.GetEndPoint(0).Y, z);
                    p1 = new XYZ(maxX, line.GetEndPoint(0).Y, z);
                }
                else
                {
                    p0 = new XYZ(line.GetEndPoint(0).X, minY, z);
                    p1 = new XYZ(line.GetEndPoint(0).X, maxY, z);
                }

                if (p0.DistanceTo(p1) <= Constants.MIN_DISTANCE_TOLERANCE)
                    continue;

                try
                {
                    SetModelExtent(level, view, DatumEnds.End0);
                    SetModelExtent(level, view, DatumEnds.End1);
                    Line newLine = Line.CreateBound(p0, p1);
                    level.SetCurveInView(DatumExtentType.Model, view, newLine);
                    anyApplied = true;
                }
                catch
                {
                    // skip this view
                }
            }

            return anyApplied;
        }

        private static void SetModelExtent(Level level, View view, DatumEnds end)
        {
            try { level.SetDatumExtentType(end, view, DatumExtentType.Model); }
            catch { }
        }

        private static bool TryGetCurve(Level level, View view, DatumExtentType extentType, out Curve curve)
        {
            curve = null;
            try
            {
                IList<Curve> curves = level.GetCurvesInView(extentType, view);
                if (curves == null || curves.Count == 0) return false;
                curve = curves.FirstOrDefault();
                return curve != null && curve.IsBound;
            }
            catch { return false; }
        }

        private static Result Fail(string title, string message)
        {
            DialogHelper.ShowError(title, message);
            return Result.Failed;
        }
    }
}
