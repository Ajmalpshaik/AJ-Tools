// Tool Name: Ceiling Magnet
// Description: Pick a ceiling, pick one grid intersection on it as the anchor, then continuously pick elements one-by-one to snap each to the nearest tile center. Press Esc to finish.
// Author: Ajmal P.S.
// Version: 4.1.0
// Last Updated: 2026-04-12
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Direct C# port of working pyRevit logic.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdCeilingMagnet : IExternalCommand
    {
        private const string ToolTitle = "Ceiling Magnet";
        private const double FallbackTileMm = 600.0;
        private const double MoveTolerance = 1e-9;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            try
            {
                // 1) Pick ceiling
                Reference ceilingRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new CeilingSelectionFilter(),
                    "Select ceiling");
                Ceiling ceiling = doc.GetElement(ceilingRef.ElementId) as Ceiling;
                if (ceiling == null)
                {
                    DialogHelper.ShowError(ToolTitle, "Please select a valid ceiling.");
                    return Result.Cancelled;
                }

                // 1b) Read the real tile size + rotation from the ceiling's surface pattern.
                double tileU, tileV, gridAngle;
                bool usedFallback = !TryGetCeilingTilePattern(doc, ceiling, out tileU, out tileV, out gridAngle);
                if (usedFallback)
                {
                    tileU = tileV = UnitUtils.ConvertToInternalUnits(FallbackTileMm, DisplayUnitType.DUT_MILLIMETERS);
                    gridAngle = 0.0;
                }
                if (tileU <= 0 || tileV <= 0)
                {
                    DialogHelper.ShowError(ToolTitle, "Could not determine a valid tile size from the ceiling surface pattern.");
                    return Result.Cancelled;
                }

                // 2) Pick ONE real grid intersection on the ceiling as the anchor. This is the
                // only reliable way to know where the surface pattern is anchored in world space —
                // there is no Revit 2020 API that exposes the rendered pattern's world origin.
                // After this single click, the loop below picks elements continuously without
                // asking for the anchor again.
                XYZ originPoint;
                try
                {
                    originPoint = uidoc.Selection.PickPoint("Pick one ceiling grid intersection (anchor)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (originPoint == null)
                {
                    return Result.Cancelled;
                }

                // Build the rotated grid frame in world XY using the pattern angle.
                // axisU = spacing direction for grid 0 (perpendicular to grid-0 lines)
                // axisV = spacing direction for grid 1 (parallel to grid-0 lines)
                double cosA = Math.Cos(gridAngle);
                double sinA = Math.Sin(gridAngle);
                XYZ axisU = new XYZ(-sinA, cosA, 0);
                XYZ axisV = new XYZ(cosA, sinA, 0);

                int moved = 0;
                int aligned = 0;
                int skipped = 0;

                // 3) Snap any preselected elements first (one transaction), then enter the
                // continuous pick loop until the user presses Esc.
                using (TransactionGroup group = new TransactionGroup(doc, "Ceiling Magnet"))
                {
                    group.Start();

                    IList<Element> preselected = GetPreselectedPointElements(uidoc, doc);
                    if (preselected.Count > 0)
                    {
                        using (Transaction tx = new Transaction(doc, "Snap Preselected"))
                        {
                            tx.Start();
                            foreach (Element element in preselected)
                            {
                                SnapElement(doc, element, originPoint, axisU, axisV, tileU, tileV, ref moved, ref aligned, ref skipped);
                            }
                            tx.Commit();
                        }
                    }

                    // 4) Continuous one-by-one picking loop. Esc / right-click cancels and ends the loop.
                    while (true)
                    {
                        Reference pickedRef;
                        try
                        {
                            pickedRef = uidoc.Selection.PickObject(
                                ObjectType.Element,
                                new PointElementSelectionFilter(),
                                "Pick element to snap (Esc to finish)");
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            break;
                        }

                        if (pickedRef == null)
                        {
                            break;
                        }

                        Element element = doc.GetElement(pickedRef.ElementId);
                        if (!IsPointBasedElement(element))
                        {
                            skipped++;
                            continue;
                        }

                        using (Transaction tx = new Transaction(doc, "Snap Element"))
                        {
                            tx.Start();
                            SnapElement(doc, element, originPoint, axisU, axisV, tileU, tileV, ref moved, ref aligned, ref skipped);
                            tx.Commit();
                        }
                    }

                    group.Assimilate();
                }

                double tileUmm = UnitUtils.ConvertFromInternalUnits(tileU, DisplayUnitType.DUT_MILLIMETERS);
                double tileVmm = UnitUtils.ConvertFromInternalUnits(tileV, DisplayUnitType.DUT_MILLIMETERS);
                double angleDeg = gridAngle * 180.0 / Math.PI;
                string source = usedFallback
                    ? "fallback 600x600 (no model pattern on ceiling)"
                    : $"ceiling pattern {tileUmm:0.#} x {tileVmm:0.#} mm @ {angleDeg:0.##}°";

                DialogHelper.ShowInfo(
                    ToolTitle,
                    $"Grid: {source}\nMoved: {moved}\nAlready aligned: {aligned}\nSkipped: {skipped}");

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static IList<Element> GetPreselectedPointElements(UIDocument uidoc, Document doc)
        {
            var result = new List<Element>();
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return result;
            }

            foreach (ElementId id in selectedIds)
            {
                Element element = doc.GetElement(id);
                if (IsPointBasedElement(element))
                {
                    result.Add(element);
                }
            }

            return result;
        }

        private static bool IsPointBasedElement(Element element)
        {
            return element?.Location is LocationPoint;
        }

        // Snaps a single point-based element to the nearest tile center, in the rotated grid frame.
        // Updates moved/aligned/skipped counters in place. Caller is responsible for the transaction.
        private static void SnapElement(
            Document doc,
            Element element,
            XYZ originPoint,
            XYZ axisU,
            XYZ axisV,
            double tileU,
            double tileV,
            ref int moved,
            ref int aligned,
            ref int skipped)
        {
            LocationPoint location = element?.Location as LocationPoint;
            if (location == null || element.Pinned)
            {
                skipped++;
                return;
            }

            XYZ current = location.Point;
            XYZ rel = current - originPoint;
            double u = rel.DotProduct(axisU);
            double v = rel.DotProduct(axisV);
            double uSnap = NearestTileCenter1D(u, tileU);
            double vSnap = NearestTileCenter1D(v, tileV);
            XYZ delta = axisU.Multiply(uSnap).Add(axisV.Multiply(vSnap));
            // Keep the element's existing Z; only snap in the ceiling's local plane.
            XYZ target = new XYZ(originPoint.X + delta.X, originPoint.Y + delta.Y, current.Z);
            XYZ move = target - current;

            if (move.GetLength() > MoveTolerance)
            {
                ElementTransformUtils.MoveElement(doc, element.Id, move);
                moved++;
            }
            else
            {
                aligned++;
            }
        }

        // Snap a 1-D coordinate (already expressed relative to the picked origin) to the
        // nearest tile center. Tile centers sit at step/2 + n*step from the origin.
        private static double NearestTileCenter1D(double value, double step)
        {
            double n = Math.Round((value - (step * 0.5)) / step);
            return (step * 0.5) + (n * step);
        }

        // Reads the actual tile size (tileU, tileV in feet) and rotation angle (radians)
        // from the first model surface pattern found on the ceiling type's compound structure.
        // tileU = spacing perpendicular to grid-0 lines, tileV = spacing perpendicular to grid-1 lines.
        private static bool TryGetCeilingTilePattern(Document doc, Ceiling ceiling, out double tileU, out double tileV, out double angle)
        {
            tileU = 0;
            tileV = 0;
            angle = 0;

            CeilingType ceilingType = doc.GetElement(ceiling.GetTypeId()) as CeilingType;
            CompoundStructure cs = ceilingType?.GetCompoundStructure();
            if (cs == null)
            {
                return false;
            }

            foreach (CompoundStructureLayer layer in cs.GetLayers())
            {
                Material material = doc.GetElement(layer.MaterialId) as Material;
                if (material == null)
                {
                    continue;
                }

                ElementId patternId = material.SurfaceForegroundPatternId;
                if (patternId == null || patternId == ElementId.InvalidElementId)
                {
                    continue;
                }

                FillPatternElement fpe = doc.GetElement(patternId) as FillPatternElement;
                FillPattern pattern = fpe?.GetFillPattern();
                if (pattern == null || pattern.Target != FillPatternTarget.Model)
                {
                    continue;
                }

                IList<FillGrid> grids = pattern.GetFillGrids();
                if (grids == null || grids.Count < 2)
                {
                    continue;
                }

                if (grids[0].Offset <= 0 || grids[1].Offset <= 0)
                {
                    continue;
                }

                tileU = grids[0].Offset;
                tileV = grids[1].Offset;
                angle = grids[0].Angle;
                return true;
            }

            return false;
        }

        private sealed class CeilingSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem?.Category != null &&
                       elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Ceilings;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }

        private sealed class PointElementSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return IsPointBasedElement(elem);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}
