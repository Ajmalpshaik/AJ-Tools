#region Metadata
/*
 * Tool Name     : Elements to Ceiling Grid (Ceiling Magnet)
 * File Name     : CeilingMagnetService.cs
 * Purpose       : Implements the ceiling-grid-detection and snap algorithm: real-grid-geometry
 *                 detection (Revit 2025.3+), clustering grid lines into two perpendicular families,
 *                 median inter-line spacing, 2D line intersection, surface-pattern tile reading,
 *                 per-element nearest-tile-center snap math, and (as of v1.1.0) filtering a batch of
 *                 elements down to the ones that geometrically sit over a given ceiling.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-07-17
 * Last Updated  : 2026-07-20
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (RevitCompat, CeilingGridApiCompat, DialogHelper)
 *
 * Input         : A resolved CeilingSelection (host or linked ceiling + transform), a batch of
 *                 point-based elements to filter/snap, and per-element snap calls with a resolved
 *                 CeilingGridDefinition + origin point - all supplied by CmdCeilingMagnet.cs, which
 *                 owns the up-front element batch pick, the repeatable ceiling+point loop, and the
 *                 TransactionGroup.
 * Output        : A CeilingGridDefinition (tile size/angle/axes + where it came from), the subset of
 *                 a batch that sits over a given ceiling, and, per element, an in-place move to the
 *                 nearest tile center (tallied into a SnapSummary).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Tile size/angle fallback path converts units through
 *   RevitCompat (mm/internal), which owns the DisplayUnitType-vs-UnitTypeId switch - no direct
 *   legacy unit API is used here (2020-2027 audit, 2026-07-23).
 * - Real-grid path (2025.3+, see CeilingGridApiCompat): clusters the ceiling's actual grid lines into
 *   two perpendicular families, derives tile size/angle from each family's own inter-line spacing
 *   (median, for robustness against clipped boundary segments), and derives the anchor point by
 *   intersecting one line from each family. Falls back to the type-pattern-or-600mm-fallback method
 *   on any version, or any ceiling, where the real grid data is unavailable or ambiguous - never
 *   guesses on ambiguous data.
 * - FilterElementsOverCeiling (v1.1.0): per the Modeler mindset rule, does NOT use a rough bounding-box
 *   guess - it reads the ceiling's actual solid geometry, finds its largest horizontal (top/bottom)
 *   planar face, and tests each element's plan location against that face's real boundary
 *   (Face.Project returns null outside the trimmed face - an exact contains test, correct even for
 *   L-shaped/non-rectangular ceilings). Falls back to the ceiling's bounding box only if no usable
 *   solid geometry is found (defensive - should not happen for a normal ceiling).
 * - TryCreateGridDefinition calls DialogHelper directly on its two failure paths (unchanged from the
 *   original inline Command code) rather than returning an error for the caller to show - kept as-is
 *   during this extraction to avoid changing this method's behavior/contract; every other method
 *   here is a pure algorithm with no direct UI interaction.
 * - SnapElement does not open its own Transaction - it is called once per element inside the
 *   caller's already-open Transaction (one Transaction per ceiling+point round in the Command).
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.1.0 (2026-07-20) - Added FilterElementsOverCeiling + GetLargestHorizontalFace to support the
 *                       Command's new select-elements-first, then-repeat-ceiling+point-rounds
 *                       workflow: each round now only snaps the elements that actually sit over the
 *                       picked ceiling, found from real ceiling solid geometry (not a bounding-box
 *                       guess). No change to existing grid-detection or per-element snap math.
 * v1.0.0 (2026-07-17) - Initial extraction from CmdCeilingMagnet.cs (code review cleanup pass) - no
 *                       behavior change.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.CeilingMagnet
{
    /// <summary>Where the ceiling grid definition came from, for the summary report.</summary>
    internal enum CeilingGridSource
    {
        /// <summary>Real grid geometry read from Ceiling.GetCeilingGridLines (Revit 2025.3+).</summary>
        ExactApi,

        /// <summary>Read from the ceiling type's surface fill pattern (all versions).</summary>
        TypePattern,

        /// <summary>No usable pattern found; used the 600x600mm default (all versions).</summary>
        Fallback600
    }

    internal sealed class CeilingGridDefinition
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

    internal sealed class CeilingSelection
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

    internal sealed class SnapSummary
    {
        public int Moved { get; set; }

        public int Aligned { get; set; }

        public int Skipped { get; set; }
    }

    /// <summary>
    /// Ceiling-grid-detection and snap algorithm. Contains no direct selection/pick interaction
    /// (except the DialogHelper calls noted above, kept unchanged from the original Command code).
    /// </summary>
    internal static class CeilingMagnetService
    {
        internal const string ToolTitle = "Ceiling Magnet";
        private const double FallbackTileMm = 600.0;
        internal const double MoveTolerance = 1e-9;

        // Real-grid detection (Revit 2025.3+, see TryGetGridFromRealGeometry) tuning constants.
        private const int MinTotalGridLines = 4;
        private const double FamilyAngleCosTolerance = 0.9962; // ~5 degrees
        private const double PerpendicularityCosTolerance = 0.15; // ~8.6 degrees from perpendicular
        private const double DuplicatePositionTolerance = 0.01; // feet (~3mm) - merges clipped duplicate lines
        private const double MinPlausibleTileFeet = 0.05; // ~15mm
        private const double MaxPlausibleTileFeet = 33.0; // ~10m
        private const double SpacingConsistencyTolerance = 0.2; // 20% deviation from median allowed
        private const double MinSpacingConsistencyRatio = 0.6; // 60% of gaps must be consistent

        internal static bool TryCreateGridDefinition(CeilingSelection ceilingSelection, out CeilingGridDefinition grid)
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
        internal static bool TryGetGridFromRealGeometry(
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

        internal static bool TryBuildHostGridAxes(
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

        /// <summary>
        /// Returns the subset of <paramref name="elements"/> that geometrically sit over
        /// <paramref name="ceilingSelection"/>'s ceiling, tested against the ceiling's real solid
        /// geometry (largest horizontal face) rather than a rough bounding-box guess - correct even for
        /// L-shaped/non-rectangular ceilings. Falls back to the ceiling's bounding box only if no
        /// usable horizontal face is found.
        /// </summary>
        internal static IList<Element> FilterElementsOverCeiling(IList<Element> elements, CeilingSelection ceilingSelection)
        {
            List<Element> result = new List<Element>();
            if (elements == null || elements.Count == 0 || ceilingSelection == null)
            {
                return result;
            }

            PlanarFace horizontalFace = GetLargestHorizontalFace(ceilingSelection.Ceiling);
            BoundingBoxXYZ localBoundingBox = horizontalFace == null
                ? ceilingSelection.Ceiling.get_BoundingBox(null)
                : null;

            if (horizontalFace == null && localBoundingBox == null)
            {
                return result;
            }

            Transform inverse = (ceilingSelection.TransformToHost ?? Transform.Identity).Inverse;

            foreach (Element element in elements)
            {
                LocationPoint location = element?.Location as LocationPoint;
                if (location == null)
                {
                    continue;
                }

                XYZ localPoint = inverse.OfPoint(location.Point);
                bool isOverCeiling;
                if (horizontalFace != null)
                {
                    XYZ probe = new XYZ(localPoint.X, localPoint.Y, horizontalFace.Origin.Z);
                    isOverCeiling = horizontalFace.Project(probe) != null;
                }
                else
                {
                    isOverCeiling = localPoint.X >= localBoundingBox.Min.X && localPoint.X <= localBoundingBox.Max.X
                        && localPoint.Y >= localBoundingBox.Min.Y && localPoint.Y <= localBoundingBox.Max.Y;
                }

                if (isOverCeiling)
                {
                    result.Add(element);
                }
            }

            return result;
        }

        /// <summary>
        /// Finds the ceiling's largest horizontal (top or bottom) planar face from its real solid
        /// geometry, used by FilterElementsOverCeiling as an exact plan-boundary contains test. Returns
        /// null if the ceiling has no solid geometry with a horizontal face (defensive fallback path).
        /// </summary>
        private static PlanarFace GetLargestHorizontalFace(Ceiling ceiling)
        {
            Options options = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geometry = ceiling.get_Geometry(options);
            if (geometry == null)
            {
                return null;
            }

            PlanarFace best = null;
            double bestArea = 0;

            foreach (GeometryObject geometryObject in geometry)
            {
                Solid solid = geometryObject as Solid;
                if (solid == null)
                {
                    continue;
                }

                foreach (Face face in solid.Faces)
                {
                    PlanarFace planarFace = face as PlanarFace;
                    if (planarFace == null)
                    {
                        continue;
                    }

                    if (Math.Abs(Math.Abs(planarFace.FaceNormal.Z) - 1.0) > 0.01)
                    {
                        continue;
                    }

                    if (planarFace.Area > bestArea)
                    {
                        bestArea = planarFace.Area;
                        best = planarFace;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Snaps a single element to the nearest tile center. Does not open its own Transaction - the
        /// caller (CmdCeilingMagnet) calls this once per element inside its own already-open Transaction.
        /// </summary>
        internal static void SnapElement(
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
    }
}
