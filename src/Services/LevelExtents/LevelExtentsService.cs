#region Metadata
/*
 * Tool Name     : Match Level Extents
 * File Name     : LevelExtentsService.cs
 * Purpose       : Copies one source level's 3D extents onto target levels picked one-by-one, so the
 *                 targets match the source length across every view that shows them.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-12
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Selection - one source level, then target levels picked one-by-one (Esc to finish).
 * Output        : Each target level's 3D extents matched to the source; one undo step per target.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. GetCurvesInView / SetCurveInView / SetDatumExtentType are
 *   stable across all target versions.
 * - Project-only tool; exits cleanly in the Family Editor.
 * - Esc during a pick is a normal cancel (handled silently); normal success is silent.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-12) - Initial release.
 * v1.1.0 (2026-06-30) - Added mandatory metadata block; silent success (no result popup);
 *                       Family-Editor guard; removed dead local. Extent-matching behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
                if (doc.IsFamilyDocument)
                    return Fail(title, "This tool runs in a project, not the Family Editor.");

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

                    using (Transaction t = new Transaction(doc, "AJ Tools - Match Level Extents"))
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
                        }
                    }
                }

                if (updatedCount == 0)
                {
                    DialogHelper.ShowInfo(title, "No target levels were updated. Pick a source level, then pick shorter levels to extend.");
                    return Result.Cancelled;
                }

                // Normal success is silent - the matched levels update visibly in their views.
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
            bool anyApplied = false;
            double zOffset = targetLevel.Elevation - sourceLevel.Elevation;
            XYZ translation = new XYZ(0, 0, zOffset);
            Transform transform = Transform.CreateTranslation(translation);

            foreach (View view in datumViews)
            {
                if (!TryGetCurve(sourceLevel, view, DatumExtentType.Model, out Curve sourceCurve))
                {
                    if (!TryGetCurve(sourceLevel, view, DatumExtentType.ViewSpecific, out sourceCurve))
                        continue;
                }

                DatumExtentType targetType = DatumExtentType.Model;
                if (!TryGetCurve(targetLevel, view, DatumExtentType.Model, out Curve targetCurve))
                {
                    if (!TryGetCurve(targetLevel, view, DatumExtentType.ViewSpecific, out targetCurve))
                        continue;
                    targetType = DatumExtentType.ViewSpecific;
                }

                try
                {
                    Curve newCurve = sourceCurve.CreateTransformed(transform);
                    if (targetType == DatumExtentType.Model)
                    {
                        SetModelExtent(targetLevel, view, DatumEnds.End0);
                        SetModelExtent(targetLevel, view, DatumEnds.End1);
                    }
                    targetLevel.SetCurveInView(targetType, view, newCurve);
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
