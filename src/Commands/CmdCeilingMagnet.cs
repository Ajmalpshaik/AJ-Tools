#region Metadata
/*
 * Tool Name     : Elements to Ceiling Grid (Ceiling Magnet)
 * File Name     : CmdCeilingMagnet.cs
 * Purpose       : Snaps point-based elements to the nearest ceiling grid tile centers, using the picked
 *                 ceiling's surface pattern (or a 600x600 fallback) and one clicked grid intersection.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-12
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (DialogHelper)
 *
 * Input         : Active project - a ceiling (host or linked), one anchor grid intersection, then
 *                 point-based elements (pre-selected and/or picked one-by-one, Esc to finish).
 * Output        : Elements moved in plan to the nearest tile center; final report of moved / aligned / skipped.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Tile size/angle is read from the ceiling surface pattern; uses
 *   UnitUtils with DisplayUnitType (the Revit 2020 unit API) - revisit for 2021+ ForgeTypeId builds.
 * - Reads linked-model ceilings for reference only; never modifies linked elements.
 * - The whole session (pre-selected batch + each pick) is one TransactionGroup assimilated into a single undo step.
 * - Esc during a pick ends the session silently; pinned / non-point elements are skipped and counted.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-12) - Initial C# port of the pyRevit ceiling-snap logic.
 * v1.1.0 (2026-07-01) - Refactor/audit: standardized metadata block; ceiling selection, grid resolution
 *                       and transaction flow reviewed. Snap behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
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
        private const string TransactionGroupName = "AJ Tools - Ceiling Magnet";
        private const string SnapPreselectedTransactionName = "Snap Preselected";
        private const string SnapElementTransactionName = "Snap Element";

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
                CeilingSelection ceilingSelection;
                if (!TryPickCeilingSelection(uidoc, doc, out ceilingSelection))
                {
                    return Result.Cancelled;
                }

                CeilingGridDefinition grid;
                if (!TryCreateGridDefinition(ceilingSelection, out grid))
                {
                    return Result.Cancelled;
                }

                XYZ originPoint = PickAnchorPoint(uidoc);
                if (originPoint == null)
                {
                    return Result.Cancelled;
                }

                SnapSummary summary = SnapElementsToGrid(uidoc, doc, originPoint, grid);
                ShowSummary(grid, summary);

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

        private static bool TryPickCeilingSelection(UIDocument uidoc, Document hostDoc, out CeilingSelection selection)
        {
            selection = null;

            Reference ceilingReference = PickCeilingReference(uidoc);
            string skippedReason;
            if (TryResolveCeilingSelection(hostDoc, ceilingReference, out selection, out skippedReason))
            {
                return true;
            }

            if (CanPickLinkedElementFromReference(hostDoc, ceilingReference))
            {
                Reference linkedReference = PickLinkedCeilingReference(uidoc);
                if (TryResolveCeilingSelection(hostDoc, linkedReference, out selection, out skippedReason))
                {
                    return true;
                }
            }

            DialogHelper.ShowError(ToolTitle, skippedReason);
            return false;
        }

        private static bool TryCreateGridDefinition(CeilingSelection ceilingSelection, out CeilingGridDefinition grid)
        {
            grid = null;

            double tileU;
            double tileV;
            double gridAngle;
            bool usedFallback = !TryGetCeilingTilePattern(
                ceilingSelection.Document,
                ceilingSelection.Ceiling,
                out tileU,
                out tileV,
                out gridAngle);

            if (usedFallback)
            {
                tileU = ConvertMillimetersToInternal(FallbackTileMm);
                tileV = tileU;
                gridAngle = 0.0;
            }

            if (tileU <= 0 || tileV <= 0)
            {
                DialogHelper.ShowError(ToolTitle, "Could not determine a valid tile size from the ceiling surface pattern.");
                return false;
            }

            XYZ axisU;
            XYZ axisV;
            if (!TryBuildHostGridAxes(gridAngle, ceilingSelection.TransformToHost, out axisU, out axisV))
            {
                DialogHelper.ShowError(ToolTitle, "Could not convert the selected ceiling grid direction to host coordinates.");
                return false;
            }

            grid = new CeilingGridDefinition(tileU, tileV, axisU, axisV, usedFallback);
            return true;
        }

        private static XYZ PickAnchorPoint(UIDocument uidoc)
        {
            // Revit 2020 does not expose the rendered model pattern origin, so the anchor
            // still has to come from one manual click on a visible grid intersection.
            return uidoc.Selection.PickPoint("Pick one ceiling grid intersection (anchor)");
        }

        private static SnapSummary SnapElementsToGrid(UIDocument uidoc, Document doc, XYZ originPoint, CeilingGridDefinition grid)
        {
            SnapSummary summary = new SnapSummary();

            using (TransactionGroup group = new TransactionGroup(doc, TransactionGroupName))
            {
                group.Start();

                IList<Element> preselected = GetPreselectedPointElements(uidoc, doc);
                if (preselected.Count > 0)
                {
                    using (Transaction tx = new Transaction(doc, SnapPreselectedTransactionName))
                    {
                        tx.Start();
                        foreach (Element element in preselected)
                        {
                            SnapElement(doc, element, originPoint, grid, summary);
                        }

                        tx.Commit();
                    }
                }

                while (true)
                {
                    Reference pickedReference;
                    try
                    {
                        pickedReference = uidoc.Selection.PickObject(
                            ObjectType.Element,
                            new PointElementSelectionFilter(),
                            "Pick element to snap (Esc to finish)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    Element element = doc.GetElement(pickedReference.ElementId);
                    using (Transaction tx = new Transaction(doc, SnapElementTransactionName))
                    {
                        tx.Start();
                        SnapElement(doc, element, originPoint, grid, summary);
                        tx.Commit();
                    }
                }

                group.Assimilate();
            }

            return summary;
        }

        private static void ShowSummary(CeilingGridDefinition grid, SnapSummary summary)
        {
            double tileUmm = ConvertInternalToMillimeters(grid.TileU);
            double tileVmm = ConvertInternalToMillimeters(grid.TileV);
            double angleDeg = Math.Atan2(grid.AxisV.Y, grid.AxisV.X) * 180.0 / Math.PI;
            string source = grid.UsedFallback
                ? "fallback 600x600 (no model pattern on ceiling)"
                : string.Format("ceiling pattern {0:0.#} x {1:0.#} mm @ {2:0.##} deg", tileUmm, tileVmm, angleDeg);

            DialogHelper.ShowInfo(
                ToolTitle,
                string.Format(
                    "Grid: {0}\nMoved: {1}\nAlready aligned: {2}\nSkipped: {3}",
                    source,
                    summary.Moved,
                    summary.Aligned,
                    summary.Skipped));
        }

        private static Reference PickCeilingReference(UIDocument uidoc)
        {
            return uidoc.Selection.PickObject(
                ObjectType.PointOnElement,
                "Select ceiling from current model or linked model");
        }

        private static Reference PickLinkedCeilingReference(UIDocument uidoc)
        {
            return uidoc.Selection.PickObject(
                ObjectType.LinkedElement,
                "Select ceiling inside the linked model");
        }

        private static bool TryResolveCeilingSelection(
            Document hostDoc,
            Reference reference,
            out CeilingSelection selection,
            out string skippedReason)
        {
            selection = null;
            skippedReason = string.Empty;

            if (hostDoc == null || reference == null)
            {
                skippedReason = "No ceiling was selected.";
                return false;
            }

            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                RevitLinkInstance linkInstance = hostDoc.GetElement(reference.ElementId) as RevitLinkInstance;
                if (linkInstance == null)
                {
                    skippedReason = "The selected linked ceiling reference is not associated with a valid Revit link instance.";
                    return false;
                }

                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null)
                {
                    skippedReason = "The selected linked model is unloaded. Load the link and try again.";
                    return false;
                }

                Ceiling linkedCeiling = linkDoc.GetElement(reference.LinkedElementId) as Ceiling;
                if (linkedCeiling == null)
                {
                    skippedReason = "Skipped: the selected linked element is not a ceiling.";
                    return false;
                }

                selection = new CeilingSelection(linkDoc, linkedCeiling, linkInstance.GetTotalTransform());
                return true;
            }

            Element hostElement = hostDoc.GetElement(reference.ElementId);
            Ceiling hostCeiling = hostElement as Ceiling;
            if (hostCeiling == null)
            {
                RevitLinkInstance linkInstance = hostElement as RevitLinkInstance;
                if (linkInstance != null && linkInstance.GetLinkDocument() == null)
                {
                    skippedReason = "The selected linked model is unloaded. Load the link and try again.";
                }
                else
                {
                    skippedReason = linkInstance != null
                        ? "Select the ceiling inside the linked model, not only the link instance."
                        : "Skipped: the selected host element is not a ceiling.";
                }

                return false;
            }

            selection = new CeilingSelection(hostDoc, hostCeiling, Transform.Identity);
            return true;
        }

        private static bool CanPickLinkedElementFromReference(Document doc, Reference reference)
        {
            if (doc == null || reference == null || reference.ElementId == ElementId.InvalidElementId)
            {
                return false;
            }

            RevitLinkInstance linkInstance = doc.GetElement(reference.ElementId) as RevitLinkInstance;
            return linkInstance != null && linkInstance.GetLinkDocument() != null;
        }

        private static bool TryBuildHostGridAxes(
            double gridAngle,
            Transform transformToHost,
            out XYZ axisU,
            out XYZ axisV)
        {
            double cosA = Math.Cos(gridAngle);
            double sinA = Math.Sin(gridAngle);
            XYZ localAxisU = new XYZ(-sinA, cosA, 0);
            XYZ localAxisV = new XYZ(cosA, sinA, 0);
            Transform transform = transformToHost ?? Transform.Identity;

            bool hasAxisU = TryProjectAndNormalize(transform.OfVector(localAxisU), out axisU);
            bool hasAxisV = TryProjectAndNormalize(transform.OfVector(localAxisV), out axisV);
            return hasAxisU && hasAxisV;
        }

        private static bool TryProjectAndNormalize(XYZ vector, out XYZ normalized)
        {
            normalized = null;
            if (vector == null)
            {
                return false;
            }

            XYZ projected = new XYZ(vector.X, vector.Y, 0);
            double length = projected.GetLength();
            if (length <= MoveTolerance)
            {
                return false;
            }

            normalized = new XYZ(projected.X / length, projected.Y / length, 0);
            return true;
        }

        private static IList<Element> GetPreselectedPointElements(UIDocument uidoc, Document doc)
        {
            List<Element> result = new List<Element>();
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

        private static void SnapElement(
            Document doc,
            Element element,
            XYZ originPoint,
            CeilingGridDefinition grid,
            SnapSummary summary)
        {
            LocationPoint location = element?.Location as LocationPoint;
            if (location == null || element.Pinned)
            {
                summary.Skipped++;
                return;
            }

            XYZ current = location.Point;
            XYZ rel = current - originPoint;
            double u = rel.DotProduct(grid.AxisU);
            double v = rel.DotProduct(grid.AxisV);
            double uSnap = NearestTileCenter1D(u, grid.TileU);
            double vSnap = NearestTileCenter1D(v, grid.TileV);
            XYZ delta = grid.AxisU.Multiply(uSnap).Add(grid.AxisV.Multiply(vSnap));
            XYZ target = new XYZ(originPoint.X + delta.X, originPoint.Y + delta.Y, current.Z);
            XYZ move = target - current;

            if (move.GetLength() > MoveTolerance)
            {
                ElementTransformUtils.MoveElement(doc, element.Id, move);
                summary.Moved++;
            }
            else
            {
                summary.Aligned++;
            }
        }

        private static double NearestTileCenter1D(double value, double step)
        {
            double n = Math.Round((value - (step * 0.5)) / step);
            return (step * 0.5) + (n * step);
        }

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

        private static double ConvertMillimetersToInternal(double value)
        {
#if REVIT2022_OR_GREATER
            return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
#else
            return UnitUtils.ConvertToInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS);
#endif
        }

        private static double ConvertInternalToMillimeters(double value)
        {
#if REVIT2022_OR_GREATER
            return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters);
#else
            return UnitUtils.ConvertFromInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS);
#endif
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

        private sealed class CeilingSelection
        {
            public CeilingSelection(Document document, Ceiling ceiling, Transform transformToHost)
            {
                Document = document;
                Ceiling = ceiling;
                TransformToHost = transformToHost ?? Transform.Identity;
            }

            public Document Document { get; }

            public Ceiling Ceiling { get; }

            public Transform TransformToHost { get; }
        }

        private sealed class CeilingGridDefinition
        {
            public CeilingGridDefinition(double tileU, double tileV, XYZ axisU, XYZ axisV, bool usedFallback)
            {
                TileU = tileU;
                TileV = tileV;
                AxisU = axisU;
                AxisV = axisV;
                UsedFallback = usedFallback;
            }

            public double TileU { get; }

            public double TileV { get; }

            public XYZ AxisU { get; }

            public XYZ AxisV { get; }

            public bool UsedFallback { get; }
        }

        private sealed class SnapSummary
        {
            public int Moved { get; set; }

            public int Aligned { get; set; }

            public int Skipped { get; set; }
        }
    }
}
