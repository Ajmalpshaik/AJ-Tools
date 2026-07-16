#region Metadata
/*
 * Tool Name     : Elements to Ceiling Grid (Ceiling Magnet)
 * File Name     : CmdCeilingMagnet.cs
 * Purpose       : Snaps point-based elements to the nearest ceiling grid tile centers. On Revit 2025.3+
 *                 reads the ceiling's real grid line geometry directly (exact, no click needed). On
 *                 older versions, falls back to the ceiling's surface pattern (or a 600x600 fallback)
 *                 plus one clicked grid intersection - unchanged from the original behaviour.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-04-12
 * Last Updated  : 2026-07-07
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
 * - Real-grid path (2025.3+, see CeilingGridApiCompat): clusters the ceiling's actual grid lines into
 *   two perpendicular families, derives tile size/angle from each family's own inter-line spacing (median,
 *   for robustness against clipped boundary segments), and derives the anchor point by intersecting one
 *   line from each family - so the manual PickAnchorPoint click is skipped entirely on those versions.
 *   Falls back to the original type-pattern-or-fallback + manual-click method on any version, or any
 *   ceiling, where the real grid data is unavailable or ambiguous (never guesses on ambiguous data).
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-12) - Initial C# port of the pyRevit ceiling-snap logic.
 * v1.1.0 (2026-07-01) - Refactor/audit: standardized metadata block; ceiling selection, grid resolution
 *                       and transaction flow reviewed. Snap behaviour unchanged.
 * v1.2.0 (2026-07-07) - Added a Revit 2025.3+ real-grid detection path (Ceiling.GetCeilingGridLines):
 *                       exact tile size/angle/anchor from the ceiling's actual grid, no manual click
 *                       needed. 2020-2024 (and any ceiling without usable grid data) behaviour unchanged.
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
        private const string TransactionGroupName = "Ceiling Magnet";
        private const string SnapPreselectedTransactionName = "Snap Preselected";
        private const string SnapElementTransactionName = "Snap Element";

        // Real-grid detection (Revit 2025.3+, see TryGetGridFromRealGeometry) tuning constants.
        private const int MinTotalGridLines = 4;
        private const double FamilyAngleCosTolerance = 0.9962; // ~5 degrees
        private const double PerpendicularityCosTolerance = 0.15; // ~8.6 degrees from perpendicular
        private const double DuplicatePositionTolerance = 0.01; // feet (~3mm) - merges clipped duplicate lines
        private const double MinPlausibleTileFeet = 0.05; // ~15mm
        private const double MaxPlausibleTileFeet = 33.0; // ~10m
        private const double SpacingConsistencyTolerance = 0.2; // 20% deviation from median allowed
        private const double MinSpacingConsistencyRatio = 0.6; // 60% of gaps must be consistent

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
                XYZ originPoint;
                if (!TryGetGridFromRealGeometry(ceilingSelection, out grid, out originPoint))
                {
                    if (!TryCreateGridDefinition(ceilingSelection, out grid))
                    {
                        return Result.Cancelled;
                    }

                    originPoint = PickAnchorPoint(uidoc);
                    if (originPoint == null)
                    {
                        return Result.Cancelled;
                    }
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
                tileU = RevitCompat.MmToInternal(FallbackTileMm);
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

            CeilingGridSource source = usedFallback ? CeilingGridSource.Fallback600 : CeilingGridSource.TypePattern;
            grid = new CeilingGridDefinition(tileU, tileV, axisU, axisV, source);
            return true;
        }

        /// <summary>
        /// Revit 2025.3+: derives the grid definition and anchor point directly from the ceiling's real
        /// grid line geometry (Ceiling.GetCeilingGridLines), skipping the manual anchor click entirely.
        /// Returns false on any older version, or whenever the real grid data is missing/ambiguous - the
        /// caller then falls back to TryCreateGridDefinition + PickAnchorPoint, unchanged.
        /// </summary>
        private static bool TryGetGridFromRealGeometry(
            CeilingSelection ceilingSelection,
            out CeilingGridDefinition grid,
            out XYZ originPoint)
        {
            grid = null;
            originPoint = null;

            IList<Curve> curves;
            if (!CeilingGridApiCompat.TryGetGridLines(ceilingSelection.Ceiling, out curves))
            {
                return false;
            }

            List<Line> lines = new List<Line>();
            foreach (Curve curve in curves)
            {
                Line line = curve as Line;
                if (line != null && line.Length > MoveTolerance)
                {
                    lines.Add(line);
                }
            }

            if (lines.Count < MinTotalGridLines)
            {
                return false;
            }

            List<Line> familyA;
            List<Line> familyB;
            if (!TryClusterIntoTwoFamilies(lines, out familyA, out familyB))
            {
                return false;
            }

            XYZ localAxisV = familyA[0].Direction;
            XYZ localAxisU = new XYZ(-localAxisV.Y, localAxisV.X, 0);

            double tileU;
            double tileV;
            if (!TryGetFamilySpacing(familyA, localAxisU, out tileU) ||
                !TryGetFamilySpacing(familyB, localAxisV, out tileV))
            {
                return false;
            }

            XYZ localOrigin;
            if (!TryIntersect2D(familyA[0], familyB[0], out localOrigin))
            {
                return false;
            }

            Transform transform = ceilingSelection.TransformToHost ?? Transform.Identity;

            XYZ axisU;
            XYZ axisV;
            if (!TryProjectAndNormalize(transform.OfVector(localAxisU), out axisU) ||
                !TryProjectAndNormalize(transform.OfVector(localAxisV), out axisV))
            {
                return false;
            }

            originPoint = transform.OfPoint(localOrigin);
            grid = new CeilingGridDefinition(tileU, tileV, axisU, axisV, CeilingGridSource.ExactApi);
            return true;
        }

        /// <summary>
        /// Groups near-parallel lines (mod-pi angle) into exactly two roughly-perpendicular families.
        /// Returns false if the lines don't cleanly form two such families - ambiguous data is never
        /// guessed at, the caller falls back to the existing detection method instead.
        /// </summary>
        private static bool TryClusterIntoTwoFamilies(IList<Line> lines, out List<Line> familyA, out List<Line> familyB)
        {
            familyA = new List<Line> { lines[0] };
            familyB = new List<Line>();
            XYZ refA = ToModPiDirection(lines[0].Direction);
            XYZ refB = null;

            for (int i = 1; i < lines.Count; i++)
            {
                XYZ dir = ToModPiDirection(lines[i].Direction);
                if (dir.DotProduct(refA) >= FamilyAngleCosTolerance)
                {
                    familyA.Add(lines[i]);
                }
                else if (refB == null)
                {
                    refB = dir;
                    familyB.Add(lines[i]);
                }
                else if (dir.DotProduct(refB) >= FamilyAngleCosTolerance)
                {
                    familyB.Add(lines[i]);
                }
                else
                {
                    // A third distinct direction - the grid isn't a clean two-family orthogonal
                    // pattern. Don't guess; fall back to the existing detection method.
                    return false;
                }
            }

            if (refB == null || familyA.Count < 2 || familyB.Count < 2)
            {
                return false;
            }

            // Families must be roughly perpendicular (within ~8.6 degrees of 90) - a standard ceiling
            // grid always is; anything looser isn't reliable enough to snap against automatically.
            return Math.Abs(refA.DotProduct(refB)) <= PerpendicularityCosTolerance;
        }

        /// <summary>
        /// Normalizes a direction to a stable mod-pi representative so a line and its reverse cluster
        /// together (flips sign so X is non-negative, or Y is non-negative when X is ~0).
        /// </summary>
        private static XYZ ToModPiDirection(XYZ direction)
        {
            if (direction.X < -MoveTolerance || (Math.Abs(direction.X) <= MoveTolerance && direction.Y < 0))
            {
                return new XYZ(-direction.X, -direction.Y, 0);
            }

            return new XYZ(direction.X, direction.Y, 0);
        }

        /// <summary>
        /// Computes a family's own inter-line spacing: projects each line's midpoint onto
        /// <paramref name="perpendicularAxis"/>, de-duplicates near-identical positions (clipped/duplicate
        /// segments on the same grid line), then takes the median consecutive difference. Rejects the
        /// result if the spacing is implausible or the family's spacing isn't consistent enough to trust.
        /// </summary>
        private static bool TryGetFamilySpacing(IList<Line> family, XYZ perpendicularAxis, out double spacing)
        {
            spacing = 0;

            List<double> positions = new List<double>();
            foreach (Line line in family)
            {
                XYZ midpoint = line.Evaluate(0.5, true);
                positions.Add(midpoint.DotProduct(perpendicularAxis));
            }

            positions.Sort();

            List<double> distinct = new List<double>();
            foreach (double position in positions)
            {
                if (distinct.Count == 0 || position - distinct[distinct.Count - 1] > DuplicatePositionTolerance)
                {
                    distinct.Add(position);
                }
            }

            if (distinct.Count < 2)
            {
                return false;
            }

            List<double> deltas = new List<double>();
            for (int i = 1; i < distinct.Count; i++)
            {
                deltas.Add(distinct[i] - distinct[i - 1]);
            }

            deltas.Sort();
            double median = deltas[deltas.Count / 2];

            if (median < MinPlausibleTileFeet || median > MaxPlausibleTileFeet)
            {
                return false;
            }

            int consistent = 0;
            foreach (double delta in deltas)
            {
                if (Math.Abs(delta - median) <= median * SpacingConsistencyTolerance)
                {
                    consistent++;
                }
            }

            if (consistent < deltas.Count * MinSpacingConsistencyRatio)
            {
                return false;
            }

            spacing = median;
            return true;
        }

        /// <summary>
        /// Intersects the infinite carrier lines of <paramref name="lineA"/> and <paramref name="lineB"/>
        /// in the XY plane. Used only as a reference anchor for the conceptual infinite snap grid, so the
        /// intersection does not need to fall within either line's drawn extents.
        /// </summary>
        private static bool TryIntersect2D(Line lineA, Line lineB, out XYZ intersection)
        {
            intersection = null;

            XYZ p1 = lineA.GetEndPoint(0);
            XYZ d1 = lineA.Direction;
            XYZ p2 = lineB.GetEndPoint(0);
            XYZ d2 = lineB.Direction;

            double denom = (d1.X * d2.Y) - (d1.Y * d2.X);
            if (Math.Abs(denom) <= MoveTolerance)
            {
                return false;
            }

            double t = (((p2.X - p1.X) * d2.Y) - ((p2.Y - p1.Y) * d2.X)) / denom;
            intersection = new XYZ(p1.X + (d1.X * t), p1.Y + (d1.Y * t), 0);
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
            double tileUmm = RevitCompat.InternalToMm(grid.TileU);
            double tileVmm = RevitCompat.InternalToMm(grid.TileV);
            double angleDeg = Math.Atan2(grid.AxisV.Y, grid.AxisV.X) * 180.0 / Math.PI;
            string source;
            switch (grid.Source)
            {
                case CeilingGridSource.ExactApi:
                    source = string.Format("exact ceiling grid {0:0.#} x {1:0.#} mm @ {2:0.##} deg (from model, no click needed)", tileUmm, tileVmm, angleDeg);
                    break;
                case CeilingGridSource.Fallback600:
                    source = "fallback 600x600 (no model pattern on ceiling)";
                    break;
                default:
                    source = string.Format("ceiling pattern {0:0.#} x {1:0.#} mm @ {2:0.##} deg", tileUmm, tileVmm, angleDeg);
                    break;
            }

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

        /// <summary>Where the ceiling grid definition came from, for the summary report.</summary>
        private enum CeilingGridSource
        {
            /// <summary>Real grid geometry read from Ceiling.GetCeilingGridLines (Revit 2025.3+).</summary>
            ExactApi,

            /// <summary>Read from the ceiling type's surface fill pattern (all versions).</summary>
            TypePattern,

            /// <summary>No usable pattern found; used the 600x600mm default (all versions).</summary>
            Fallback600
        }

        private sealed class CeilingGridDefinition
        {
            public CeilingGridDefinition(double tileU, double tileV, XYZ axisU, XYZ axisV, CeilingGridSource source)
            {
                TileU = tileU;
                TileV = tileV;
                AxisU = axisU;
                AxisV = axisV;
                Source = source;
            }

            public double TileU { get; }

            public double TileV { get; }

            public XYZ AxisU { get; }

            public XYZ AxisV { get; }

            public CeilingGridSource Source { get; }
        }

        private sealed class SnapSummary
        {
            public int Moved { get; set; }

            public int Aligned { get; set; }

            public int Skipped { get; set; }
        }
    }
}
