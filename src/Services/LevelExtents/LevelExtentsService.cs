// Tool Name: Level Extents Service
// Description: Copies one source level 3D/model extents to shorter target levels picked one-by-one.
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
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Services.LevelExtents
{
    /// <summary>
    /// Applies source level 3D/model extents to picked shorter target levels.
    /// </summary>
    internal static class LevelExtentsService
    {
        internal static Result Execute(ExternalCommandData commandData, string title)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                    return Fail(title, "Open a project view before running this command.");

                Document doc = uidoc.Document;
                View view = doc.ActiveView;
                if (view == null || view.IsTemplate)
                    return Fail(title, "Please run this tool in a normal project view.");

                Level sourceLevel;
                try
                {
                    sourceLevel = PickSourceLevel(uidoc, doc);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (sourceLevel == null)
                    return Result.Cancelled;

                List<View> datumViews = CollectDatumViewsForLevel(doc, sourceLevel);
                if (datumViews.Count == 0)
                    return Fail(title, "Could not find any 3D/elevation/section view that shows the source level.");

                int updatedCount = 0;
                int skippedCount = 0;

                while (true)
                {
                    Level targetLevel;
                    try
                    {
                        targetLevel = PickTargetLevel(uidoc, doc);
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    if (targetLevel == null)
                        continue;

                    if (targetLevel.Id == sourceLevel.Id)
                        continue;

                    using (Transaction t = new Transaction(doc, "AJ Tools - Extend Level"))
                    {
                        t.Start();

                        if (TryApplySourceExtentsAllViews(sourceLevel, targetLevel, datumViews))
                        {
                            t.Commit();
                            updatedCount++;
                        }
                        else
                        {
                            t.RollBack();
                            skippedCount++;
                        }
                    }
                }

                if (updatedCount == 0)
                {
                    DialogHelper.ShowInfo(title, "No target levels were updated.");
                    return Result.Cancelled;
                }

                string summary = $"Updated {updatedCount} level(s) to match 3D extents of \"{sourceLevel.Name}\".";
                if (skippedCount > 0)
                    summary += $"\nSkipped {skippedCount} level(s) (already longer/equal or could not update).";

                DialogHelper.ShowInfo(title, summary);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(title, ex.Message);
                return Result.Failed;
            }
        }

        private static Level PickSourceLevel(UIDocument uidoc, Document doc)
        {
            Reference sourceRef = uidoc.Selection.PickObject(
                ObjectType.Element,
                new LevelSelectionFilter(),
                "Select SOURCE level (maximum 3D extent)");

            return doc.GetElement(sourceRef) as Level;
        }

        private static Level PickTargetLevel(UIDocument uidoc, Document doc)
        {
            Reference targetRef = uidoc.Selection.PickObject(
                ObjectType.Element,
                new LevelSelectionFilter(),
                "Select TARGET level to apply source 3D extent (Esc to finish)");

            return doc.GetElement(targetRef) as Level;
        }

        private static bool TryApplySourceExtentsAllViews(Level sourceLevel, Level targetLevel, List<View> datumViews)
        {
            // Step 1: compute source level's full XY bounds by unioning every readable curve.
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            bool gotAny = false;

            foreach (View view in datumViews)
            {
                if (!TryGetCurve(sourceLevel, view, DatumExtentType.Model, out Curve c) &&
                    !TryGetCurve(sourceLevel, view, DatumExtentType.ViewSpecific, out c))
                    continue;

                XYZ a = c.GetEndPoint(0);
                XYZ b = c.GetEndPoint(1);
                if (a.X < minX) minX = a.X;
                if (b.X < minX) minX = b.X;
                if (a.X > maxX) maxX = a.X;
                if (b.X > maxX) maxX = b.X;
                if (a.Y < minY) minY = a.Y;
                if (b.Y < minY) minY = b.Y;
                if (a.Y > maxY) maxY = a.Y;
                if (b.Y > maxY) maxY = b.Y;
                gotAny = true;
            }

            if (!gotAny) return false;

            // Step 2: apply the bounds to the target in every datum view, choosing X or Y based on the existing line direction.
            bool anyApplied = false;
            double z = targetLevel.Elevation;

            foreach (View view in datumViews)
            {
                DatumExtentType extentType = DatumExtentType.Model;
                if (!TryGetCurve(targetLevel, view, DatumExtentType.Model, out Curve existing))
                {
                    if (!TryGetCurve(targetLevel, view, DatumExtentType.ViewSpecific, out existing))
                        continue;
                    extentType = DatumExtentType.ViewSpecific;
                }

                if (!(existing is Line line))
                    continue;

                XYZ dir = line.Direction;
                XYZ p0, p1;
                if (Math.Abs(dir.X) >= Math.Abs(dir.Y))
                {
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
                    if (extentType == DatumExtentType.Model)
                    {
                        SetModelExtent(targetLevel, view, DatumEnds.End0);
                        SetModelExtent(targetLevel, view, DatumEnds.End1);
                    }
                    Line newLine = Line.CreateBound(p0, p1);
                    targetLevel.SetCurveInView(extentType, view, newLine);
                    anyApplied = true;
                }
                catch
                {
                    // skip this view, try next
                }
            }

            return anyApplied;
        }

        private static List<View> CollectDatumViewsForLevel(Document doc, Level level)
        {
            List<View> result = new List<View>();
            IEnumerable<View> candidates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate);

            foreach (View v in candidates)
            {
                if (TryGetCurve(level, v, DatumExtentType.Model, out _) ||
                    TryGetCurve(level, v, DatumExtentType.ViewSpecific, out _))
                {
                    result.Add(v);
                }
            }
            return result;
        }

        private static void SetModelExtent(Level level, View view, DatumEnds end)
        {
            try
            {
                level.SetDatumExtentType(end, view, DatumExtentType.Model);
            }
            catch
            {
                // Some contexts may reject setting one end; ignore and continue.
            }
        }

        private static bool TryGetCurve(Level level, View view, DatumExtentType extentType, out Curve curve)
        {
            curve = null;

            try
            {
                IList<Curve> curves = level.GetCurvesInView(extentType, view);
                if (curves == null || curves.Count == 0)
                    return false;

                curve = curves.FirstOrDefault();
                return curve != null && curve.IsBound;
            }
            catch
            {
                return false;
            }
        }

        private static double GetCurveLength(Curve curve)
        {
            if (curve == null || !curve.IsBound)
                return 0.0;

            try
            {
                return curve.Length;
            }
            catch
            {
                return 0.0;
            }
        }

        private static Result Fail(string title, string message)
        {
            DialogHelper.ShowError(title, message);
            return Result.Failed;
        }

        private class LevelSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Level;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
