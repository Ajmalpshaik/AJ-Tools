#region Metadata
/*
 * Tool Name     : Automatic Dimensions (Grids / Levels)
 * File Name     : AutoDimensionService.cs
 * Purpose       : Places dimension rows across the grids and levels visible in a plan, section or
 *                 elevation - an individual chain through every datum and an overall first-to-last row,
 *                 on one side or both, using the datum's own extents when the view has no crop.
 *
 * Author        : Ajmal P.S.
 * Version       : 2.0.0
 *
 * Created Date  : 2025-12-11
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Dimensioning, AJTools.Services.Dimensioning,
 *                 AJTools.Utils
 *
 * Input         : Active plan/section/elevation view, plus the saved AutoDatumDimensionSettings.
 * Output        : Dimension rows in the model, and a DimensionRunReport returned to the command.
 *
 * Notes         :
 * v2.0.0 is a rewrite. Behaviour changes and the defects they fix:
 * - The crop box is no longer required. When the crop is off the rows are sized from the datums'
 *   own extents instead of refusing to run.
 * - Rows can be placed on BOTH sides (grids above and below, levels left and right).
 * - The individual chain and the overall row can each use their own dimension type.
 * - With exactly two datums the overall row is suppressed by default: it duplicated the individual
 *   chain exactly, one row further out, so every two-grid view got two identical dimensions.
 * - Each row is created independently. Previously one reference Revit refused aborted the whole run
 *   and nothing at all was placed.
 * - Grids that are curved, skewed, or sitting on top of another grid are now counted and REPORTED
 *   rather than silently dropped - the old code just ignored them.
 * - Grids and levels can be read from loaded Revit links.
 * - Datums already carrying a dimension in this view are skipped, so running twice no longer stacks a
 *   second identical set on top of the first.
 * - Everything is created inside one TransactionGroup, so a single Ctrl+Z reverses the whole run.
 *
 * Changelog     :
 * v1.0.0 (2025-12-11) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block.
 * v2.0.0 (2026-08-15) - Rewrite: settings-driven rows, both-sides placement, per-row dimension types,
 *                       no-crop fallback, linked grids/levels, duplicate protection, skip reporting,
 *                       per-row failure isolation, single undo step.
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
using AJTools.Models.Dimensioning;
using AJTools.Services.Dimensioning;
using AJTools.Utils;

namespace AJTools.Services.AutoDimension
{
    /// <summary>Which datums the command dimensions.</summary>
    internal enum AutoDimensionMode
    {
        Combined,
        GridsOnly,
        LevelsOnly
    }

    /// <summary>What a run produced.</summary>
    internal sealed class AutoDimensionRunResult
    {
        internal DimensionRunReport Report { get; set; }
        internal Result Status { get; set; }
    }

    /// <summary>
    /// Places grid and level dimension rows.
    /// </summary>
    internal static class AutoDimensionService
    {
        private const string TransactionGroupName = "AJ Tools - Automatic Dimension";
        private const string TransactionName = "Automatic Dimension Row";

        private static readonly HashSet<ViewType> PlanViews = new HashSet<ViewType>
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.EngineeringPlan
        };

        /// <summary>
        /// Runs the tool. Never shows a dialog; the command decides what to show.
        /// </summary>
        internal static AutoDimensionRunResult Execute(
            ExternalCommandData commandData,
            AutoDimensionMode mode,
            AutoDatumDimensionSettings settings,
            string title,
            out string blockingMessage)
        {
            blockingMessage = string.Empty;

            AutoDimensionRunResult runResult = new AutoDimensionRunResult
            {
                Report = new DimensionRunReport(title),
                Status = Result.Cancelled
            };

            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out blockingMessage))
                return runResult;

            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (!ValidationHelper.ValidateActiveView(view, out blockingMessage))
                return runResult;

            bool isPlan = PlanViews.Contains(view.ViewType) || IsAreaPlan(view.ViewType);
            bool isSection = view.ViewType == ViewType.Section || view.ViewType == ViewType.Elevation;

            if (!isPlan && !isSection)
            {
                blockingMessage = "This tool works in plan, section and elevation views.";
                return runResult;
            }

            AutoDatumDimensionSettings effective = settings ?? AutoDatumDimensionSettings.CreateDefault();
            effective.Normalize();

            if (effective.RequireCropBox && !view.CropBoxActive)
            {
                blockingMessage =
                    "This view has no crop box switched on. Turn on 'Crop View', or turn off " +
                    "'Only run when the view is cropped' in the Automatic Dimension settings.";
                return runResult;
            }

            bool wantGrids = mode != AutoDimensionMode.LevelsOnly &&
                             effective.GridScope != DimensionModelScope.None;
            bool wantLevels = mode != AutoDimensionMode.GridsOnly &&
                              isSection &&
                              effective.LevelScope != DimensionModelScope.None;

            if (mode == AutoDimensionMode.LevelsOnly && !isSection)
            {
                blockingMessage = "Levels can only be dimensioned in a section or elevation view.";
                return runResult;
            }

            // Distinguish "this view has none" from "you switched this off". Without this the user is
            // told the view was searched and came up empty, and goes hunting through crop, visibility
            // and worksets - the one place the message never points is the setting that caused it.
            if (!wantGrids && !wantLevels)
            {
                blockingMessage = mode == AutoDimensionMode.LevelsOnly
                    ? "Levels are set to 'Do not dimension' in the Automatic Dimension settings, so this " +
                      "button has nothing to place. Change 'Levels from' and try again."
                    : mode == AutoDimensionMode.GridsOnly
                        ? "Grids are set to 'Do not dimension' in the Automatic Dimension settings, so this " +
                          "button has nothing to place. Change 'Grids from' and try again."
                        : "Nothing is switched on for this view in the Automatic Dimension settings - " +
                          "grids are set to 'Do not dimension', and levels only apply in a section or " +
                          "elevation. Change 'Grids from' and try again.";
                return runResult;
            }

            DimensionRunReport report = runResult.Report;
            report.RecordViewProcessed();

            DimensionType individualType = ResolveType(doc, effective.IndividualDimensionTypeName, report);
            DimensionType overallType = ResolveType(doc, effective.OverallDimensionTypeName, report);

            // The crop rectangle is worked out first so linked datums can be culled to what this view
            // actually shows. A host collector is view-scoped by Revit; a link collector cannot be, so
            // without this a link's whole grid set (or a 40-storey level stack) is chained in.
            ViewFrame cropFrame = ViewFrame.BuildFromCropOnly(view);

            List<DatumEntry> gridEntries = wantGrids
                ? CollectGrids(doc, view, effective, isSection, cropFrame, report)
                : new List<DatumEntry>();

            List<DatumEntry> levelEntries = wantLevels
                ? CollectLevels(doc, view, effective, cropFrame, report)
                : new List<DatumEntry>();

            if (gridEntries.Count == 0 && levelEntries.Count == 0)
            {
                blockingMessage = report.HasActivity
                    ? report.BuildSummary()
                    : "No grids or levels were found in this view to dimension.";
                return runResult;
            }

            ViewFrame frame = ViewFrame.Build(view, gridEntries.Concat(levelEntries));

            using (TransactionGroup group = new TransactionGroup(doc, TransactionGroupName))
            {
                group.Start();

                try
                {
                    // Grids that run up the screen vary across it, so they are measured along X and
                    // their rows sit above (or below). Grids that run across the screen are the reverse.
                    List<DatumEntry> acrossScreen = gridEntries.Where(e => e.Axis == ViewAxis.AlongX).ToList();
                    List<DatumEntry> upScreen = gridEntries.Where(e => e.Axis == ViewAxis.AlongY).ToList();

                    PlaceRows(doc, view, frame, acrossScreen, ViewAxis.AlongX, effective,
                              individualType, overallType, report, "grids");

                    PlaceRows(doc, view, frame, upScreen, ViewAxis.AlongY, effective,
                              individualType, overallType, report, "grids");

                    PlaceRows(doc, view, frame, levelEntries, ViewAxis.AlongY, effective,
                              individualType, overallType, report, "levels");
                }
                catch (Exception ex)
                {
                    group.RollBack();
                    blockingMessage = "An error occurred:\n" + ex.Message;
                    runResult.Status = Result.Failed;
                    return runResult;
                }

                if (report.DimensionsCreated > 0)
                    group.Assimilate();
                else
                    group.RollBack();
            }

            runResult.Status = report.DimensionsCreated > 0 ? Result.Succeeded : Result.Cancelled;
            return runResult;
        }

        // -----------------------------------------------------------------------------------------
        // Row placement
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Places the individual chain and the overall row for one set of parallel datums, on one side
        /// or both.
        /// </summary>
        private static void PlaceRows(
            Document doc,
            View view,
            ViewFrame frame,
            IList<DatumEntry> entries,
            ViewAxis axis,
            AutoDatumDimensionSettings settings,
            DimensionType individualType,
            DimensionType overallType,
            DimensionRunReport report,
            string label)
        {
            if (entries == null || entries.Count == 0)
                return;

            List<DatumEntry> ordered = entries.OrderBy(e => e.Coord).ToList();

            // Two datums sharing a coordinate would make a zero-length segment, which Revit rejects
            // and which would take the whole row down with it.
            List<DatumEntry> spaced = new List<DatumEntry>();
            foreach (DatumEntry entry in ordered)
            {
                if (spaced.Count > 0 &&
                    Math.Abs(entry.Coord - spaced[spaced.Count - 1].Coord) <= Constants.MIN_DISTANCE_TOLERANCE)
                {
                    report.RecordSkipped("Skipped a " + label + " datum sitting on top of another");
                    continue;
                }

                spaced.Add(entry);
            }

            if (spaced.Count < 2)
            {
                report.RecordSkipped("Need at least two " + label + " in line to dimension");
                return;
            }

            // Re-running over a view that is already dimensioned should place nothing; a view where
            // only some datums happen to be referenced by an unrelated dimension should still get a
            // complete chain.
            if (spaced.All(e => e.AlreadyDimensioned))
            {
                report.RecordSkipped("Existing dimensions already cover these " + label);
                return;
            }

            double scale = Math.Max(1.0, view.Scale);
            double firstOffset = settings.FirstRowOffsetMm * Constants.MM_TO_FEET * scale;
            double spacing = settings.RowSpacingMm * Constants.MM_TO_FEET * scale;

            List<RowSide> sides = new List<RowSide>();
            if (settings.Side == DimensionPlacementSide.Both)
            {
                sides.Add(RowSide.Primary);
                sides.Add(RowSide.Secondary);
            }
            else
            {
                sides.Add(settings.Side == DimensionPlacementSide.Secondary ? RowSide.Secondary : RowSide.Primary);
            }

            // The overall row only repeats the chain when the chain is actually being drawn. With
            // "overall only" selected, suppressing it produced no dimensions at all.
            bool overallWanted = settings.CreateOverallRow &&
                                 !(settings.CreateIndividualRow &&
                                   settings.SkipOverallWhenTwoDatums &&
                                   spaced.Count == 2);

            if (settings.CreateOverallRow && !overallWanted)
                report.RecordSkipped("Overall row skipped - with only two " + label + " it repeats the chain");

            foreach (RowSide side in sides)
            {
                double rowOffset = firstOffset;

                if (settings.CreateIndividualRow)
                {
                    CreateRow(doc, view, frame, spaced, axis, side, rowOffset, individualType, report, label + " chain");
                    rowOffset += spacing;
                }

                if (overallWanted)
                {
                    List<DatumEntry> endsOnly = new List<DatumEntry> { spaced.First(), spaced.Last() };
                    CreateRow(doc, view, frame, endsOnly, axis, side, rowOffset, overallType, report, label + " overall");
                }
            }
        }

        private static void CreateRow(
            Document doc,
            View view,
            ViewFrame frame,
            IList<DatumEntry> entries,
            ViewAxis axis,
            RowSide side,
            double offset,
            DimensionType dimensionType,
            DimensionRunReport report,
            string label)
        {
            ReferenceArray references = new ReferenceArray();
            foreach (DatumEntry entry in entries)
                references.Append(entry.HostReference);

            if (references.Size < 2)
            {
                report.RecordSkipped("Not enough references for the " + label);
                return;
            }

            double min = entries.Min(e => e.Coord);
            double max = entries.Max(e => e.Coord);

            XYZ start;
            XYZ end;

            if (axis == ViewAxis.AlongX)
            {
                // Datums vary across the screen: a horizontal row above or below the drawing.
                double y = side == RowSide.Primary
                    ? frame.MaxY + offset
                    : frame.MinY - offset;

                start = frame.ToModel(min, y);
                end = frame.ToModel(max, y);
            }
            else
            {
                // Datums vary up the screen: a vertical row to the left or right of the drawing.
                double x = side == RowSide.Primary
                    ? frame.MinX - offset
                    : frame.MaxX + offset;

                start = frame.ToModel(x, min);
                end = frame.ToModel(x, max);
            }

            if (!DimensionFactory.TryCreateLine(start, end, out Line line, out string lineReason))
            {
                report.RecordFailed(null, lineReason + " (" + label + ")");
                return;
            }

            // Each row gets its own transaction so a reference Revit refuses takes down only that row.
            using (Transaction transaction = new Transaction(doc, TransactionName))
            {
                try
                {
                    transaction.Start();

                    if (!DimensionFactory.TryCreate(doc, view, line, references, dimensionType,
                                                    out Dimension _, out string reason))
                    {
                        if (transaction.HasStarted() && !transaction.HasEnded())
                            transaction.RollBack();

                        report.RecordFailed(null, reason + " (" + label + ")");
                        return;
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    if (transaction.HasStarted() && !transaction.HasEnded())
                        transaction.RollBack();

                    report.RecordFailed(null, ex.Message + " (" + label + ")");
                    return;
                }
            }

            report.RecordCreated(entries.Count);
        }

        // -----------------------------------------------------------------------------------------
        // Datum collection
        // -----------------------------------------------------------------------------------------

        private static List<DatumEntry> CollectGrids(
            Document doc,
            View view,
            AutoDatumDimensionSettings settings,
            bool isSection,
            ViewFrame cropFrame,
            DimensionRunReport report)
        {
            List<DatumEntry> entries = new List<DatumEntry>();
            HashSet<string> alreadyDimensioned = settings.SkipAlreadyDimensioned
                ? BuildDimensionedKeySet(doc, view)
                : new HashSet<string>(StringComparer.Ordinal);

            IList<DimensionSource> sources = DimensionSource.Collect(
                doc,
                settings.GridScope == DimensionModelScope.CurrentModel ||
                settings.GridScope == DimensionModelScope.CurrentAndLinked,
                settings.GridScope == DimensionModelScope.LinkedModels ||
                settings.GridScope == DimensionModelScope.CurrentAndLinked);

            string[] excluded = ParseExclusions(settings.ExcludeNameFragments);

            foreach (DimensionSource source in sources)
            {
                foreach (Grid grid in EnumerateDatums<Grid>(source, view))
                {
                    if (IsExcludedByName(grid, excluded))
                    {
                        report.RecordSkipped("Skipped grids excluded by name");
                        continue;
                    }

                    bool curved;
                    try
                    {
                        curved = grid.IsCurved;
                    }
                    catch
                    {
                        curved = false;
                    }

                    if (curved)
                    {
                        report.RecordSkipped("Skipped curved grids - only straight grids can be chained");
                        continue;
                    }

                    if (!TryGetGridLineInView(grid, view, source, isSection, out XYZ startModel, out XYZ endModel))
                    {
                        report.RecordSkipped("Skipped grids not drawn in this view");
                        continue;
                    }

                    DatumEntry entry = BuildGridEntry(view, source, grid, startModel, endModel, alreadyDimensioned, report);
                    if (entry == null)
                        continue;

                    if (source.IsLinked && !IsWithinCrop(entry, cropFrame))
                    {
                        report.RecordSkipped("Skipped linked grids outside this view's crop");
                        continue;
                    }

                    entries.Add(entry);
                }
            }

            return entries;
        }

        private static DatumEntry BuildGridEntry(
            View view,
            DimensionSource source,
            Grid grid,
            XYZ startModel,
            XYZ endModel,
            ICollection<string> alreadyDimensioned,
            DimensionRunReport report)
        {
            ViewCoords startView = ViewCoords.From(view, startModel);
            ViewCoords endView = ViewCoords.From(view, endModel);

            double dx = endView.X - startView.X;
            double dy = endView.Y - startView.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length <= Constants.ZERO_LENGTH_TOLERANCE)
            {
                report.RecordSkipped("Skipped grids that read as a point in this view");
                return null;
            }

            double ux = dx / length;
            double uy = dy / length;

            ViewAxis axis;
            double coord;

            if (Math.Abs(uy) > 1.0 - Constants.PARALLEL_TOLERANCE)
            {
                // Runs up the screen, so it varies across it.
                axis = ViewAxis.AlongX;
                coord = (startView.X + endView.X) * 0.5;
            }
            else if (Math.Abs(ux) > 1.0 - Constants.PARALLEL_TOLERANCE)
            {
                axis = ViewAxis.AlongY;
                coord = (startView.Y + endView.Y) * 0.5;
            }
            else
            {
                report.RecordSkipped("Skipped grids running at an angle to the view");
                return null;
            }

            return BuildEntry(source, grid, axis, coord, startView, endView, alreadyDimensioned, report, "grid");
        }

        private static List<DatumEntry> CollectLevels(
            Document doc,
            View view,
            AutoDatumDimensionSettings settings,
            ViewFrame cropFrame,
            DimensionRunReport report)
        {
            List<DatumEntry> entries = new List<DatumEntry>();
            HashSet<string> alreadyDimensioned = settings.SkipAlreadyDimensioned
                ? BuildDimensionedKeySet(doc, view)
                : new HashSet<string>(StringComparer.Ordinal);

            IList<DimensionSource> sources = DimensionSource.Collect(
                doc,
                settings.LevelScope == DimensionModelScope.CurrentModel ||
                settings.LevelScope == DimensionModelScope.CurrentAndLinked,
                settings.LevelScope == DimensionModelScope.LinkedModels ||
                settings.LevelScope == DimensionModelScope.CurrentAndLinked);

            string[] excluded = ParseExclusions(settings.ExcludeNameFragments);

            foreach (DimensionSource source in sources)
            {
                foreach (Level level in EnumerateDatums<Level>(source, view))
                {
                    if (IsExcludedByName(level, excluded))
                    {
                        report.RecordSkipped("Skipped levels excluded by name");
                        continue;
                    }

                    if (settings.StoryLevelsOnly && !IsStoryLevel(source.Document, level))
                    {
                        report.RecordSkipped("Skipped levels that are not building stories");
                        continue;
                    }

                    XYZ point;
                    try
                    {
                        Transform toHost = source.ToHost ?? Transform.Identity;
                        point = toHost.OfPoint(new XYZ(0.0, 0.0, level.ProjectElevation));
                    }
                    catch
                    {
                        report.RecordSkipped("Skipped levels whose elevation could not be read");
                        continue;
                    }

                    ViewCoords viewPoint = ViewCoords.From(view, point);

                    // The level's line AS DRAWN gives a real horizontal extent. Without it the only
                    // horizontal figure available is the projection of the model point (0,0,elevation),
                    // which is the same arbitrary constant for every level and, in an uncropped view,
                    // would put the row metres away from the section instead of beside it.
                    bool hasDrawnExtent = TryGetDatumCurveInView(level, view, source, out XYZ drawnStart, out XYZ drawnEnd);

                    ViewCoords a = hasDrawnExtent ? ViewCoords.From(view, drawnStart) : viewPoint;
                    ViewCoords b = hasDrawnExtent ? ViewCoords.From(view, drawnEnd) : viewPoint;

                    DatumEntry entry = BuildEntry(
                        source, level, ViewAxis.AlongY, viewPoint.Y, a, b,
                        alreadyDimensioned, report, "level");

                    if (entry == null)
                        continue;

                    entry.ContributesHorizontalExtent = hasDrawnExtent;

                    if (source.IsLinked && !IsWithinCrop(entry, cropFrame))
                    {
                        report.RecordSkipped("Skipped linked levels outside this view's crop");
                        continue;
                    }

                    entries.Add(entry);
                }
            }

            return entries;
        }

        private static DatumEntry BuildEntry(
            DimensionSource source,
            Element datum,
            ViewAxis axis,
            double coord,
            ViewCoords a,
            ViewCoords b,
            ICollection<string> alreadyDimensioned,
            DimensionRunReport report,
            string label)
        {
            Reference reference;
            try
            {
                reference = new Reference(datum);
            }
            catch
            {
                report.RecordSkipped("Skipped a " + label + " that cannot be referenced");
                return null;
            }

            Reference hostReference = source.ToHostReference(reference);
            if (hostReference == null)
            {
                report.RecordSkipped("Skipped a linked " + label + " Revit will not dimension to");
                return null;
            }

            // NOTE: a datum that already carries a dimension is FLAGGED, not dropped. Dropping it left
            // a hole in the middle of the chain - grids A,B,D,E with C removed reads 6000/12000/6000,
            // one segment silently spanning an undimensioned grid. The whole row is skipped later only
            // when every datum in it is already covered.
            string elementKey = source.BuildKey(datum.Id);
            bool alreadyCovered = alreadyDimensioned.Contains(elementKey);

            return new DatumEntry
            {
                Datum = datum,
                HostReference = hostReference,
                AlreadyDimensioned = alreadyCovered,
                Axis = axis,
                Coord = coord,
                MinExtent = Math.Min(a.X, b.X),
                MaxExtent = Math.Max(a.X, b.X),
                MinExtentY = Math.Min(a.Y, b.Y),
                MaxExtentY = Math.Max(a.Y, b.Y),
                SourceName = source.DisplayName
            };
        }

        private static IEnumerable<T> EnumerateDatums<T>(DimensionSource source, View view) where T : Element
        {
            IList<Element> elements;

            try
            {
                // A host view cannot scope a collector over a link document.
                FilteredElementCollector collector = source.IsLinked
                    ? new FilteredElementCollector(source.Document)
                    : new FilteredElementCollector(source.Document, view.Id);

                elements = collector
                    .OfClass(typeof(T))
                    .WhereElementIsNotElementType()
                    .ToElements();
            }
            catch
            {
                return Enumerable.Empty<T>();
            }

            return elements.OfType<T>().Where(e => e != null);
        }

        /// <summary>
        /// The grid's line in HOST coordinates, as it is drawn in this view.
        /// </summary>
        /// <remarks>
        /// GetCurvesInView only works for the host document, so a linked grid falls back to its model
        /// curve - which is the horizontal plan line. In a section or elevation that line projects flat
        /// (both ends at the same height), so classifying from it made every linked grid either vanish
        /// as "a point" or line up at its sketch elevation. In a section the grid is actually drawn
        /// where its plane CUTS the view, so that intersection is computed instead.
        /// </remarks>
        private static bool TryGetGridLineInView(
            Grid grid,
            View view,
            DimensionSource source,
            bool isSection,
            out XYZ start,
            out XYZ end)
        {
            start = null;
            end = null;

            IList<Curve> curves = null;

            if (!source.IsLinked)
            {
                try
                {
                    curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                }
                catch
                {
                    curves = null;
                }
            }

            Line line = curves?.OfType<Line>().FirstOrDefault();

            if (line != null)
            {
                try
                {
                    Transform hostTransform = source.ToHost ?? Transform.Identity;
                    start = hostTransform.OfPoint(line.GetEndPoint(0));
                    end = hostTransform.OfPoint(line.GetEndPoint(1));
                    return start != null && end != null;
                }
                catch
                {
                    return false;
                }
            }

            // Fallback: the model curve, pushed into host coordinates.
            XYZ modelStart;
            XYZ modelEnd;
            try
            {
                Line modelLine = grid.Curve as Line;
                if (modelLine == null)
                    return false;

                Transform toHost = source.ToHost ?? Transform.Identity;
                modelStart = toHost.OfPoint(modelLine.GetEndPoint(0));
                modelEnd = toHost.OfPoint(modelLine.GetEndPoint(1));
            }
            catch
            {
                return false;
            }

            if (modelStart == null || modelEnd == null)
                return false;

            if (!isSection)
            {
                start = modelStart;
                end = modelEnd;
                return true;
            }

            return TryGetSectionTrace(view, modelStart, modelEnd, out start, out end);
        }

        /// <summary>
        /// Where a horizontal datum line crosses a section or elevation, it is drawn as a vertical
        /// line at the crossing point. Returns false when the datum runs parallel to the view plane and
        /// so is not drawn as a line at all.
        /// </summary>
        private static bool TryGetSectionTrace(View view, XYZ lineStart, XYZ lineEnd, out XYZ start, out XYZ end)
        {
            start = null;
            end = null;

            try
            {
                XYZ direction = lineEnd - lineStart;
                if (direction.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                    return false;

                direction = direction.Normalize();

                XYZ normal = view.ViewDirection.Normalize();
                double denominator = direction.DotProduct(normal);
                if (Math.Abs(denominator) <= Constants.PARALLEL_TOLERANCE)
                    return false;

                double t = (view.Origin - lineStart).DotProduct(normal) / denominator;
                XYZ crossing = lineStart + (direction * t);

                XYZ up = view.UpDirection.Normalize();

                // Any non-zero span works: only the direction and the position are read downstream.
                start = crossing;
                end = crossing + up;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// A datum's drawn curve in this view, in host coordinates. Only available for the host
        /// document - a host view cannot describe how a linked datum is drawn.
        /// </summary>
        private static bool TryGetDatumCurveInView(
            DatumPlane datum,
            View view,
            DimensionSource source,
            out XYZ start,
            out XYZ end)
        {
            start = null;
            end = null;

            if (source.IsLinked)
                return false;

            try
            {
                IList<Curve> curves = datum.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                Line line = curves?.OfType<Line>().FirstOrDefault();
                if (line == null)
                    return false;

                start = line.GetEndPoint(0);
                end = line.GetEndPoint(1);
            }
            catch
            {
                return false;
            }

            return start != null && end != null;
        }

        /// <summary>
        /// True when a datum falls inside the view's crop along the direction it is measured in.
        /// An inactive or unreadable crop imposes no limit.
        /// </summary>
        private static bool IsWithinCrop(DatumEntry entry, ViewFrame cropFrame)
        {
            if (cropFrame == null || cropFrame.IsEmpty)
                return true;

            double tolerance = Constants.MIN_DISTANCE_TOLERANCE;

            return entry.Axis == ViewAxis.AlongX
                ? entry.Coord >= cropFrame.MinX - tolerance && entry.Coord <= cropFrame.MaxX + tolerance
                : entry.Coord >= cropFrame.MinY - tolerance && entry.Coord <= cropFrame.MaxY + tolerance;
        }

        private static bool IsStoryLevel(Document doc, Level level)
        {
            try
            {
                Parameter parameter = level.get_Parameter(BuiltInParameter.LEVEL_IS_BUILDING_STORY);
                return parameter == null || parameter.AsInteger() != 0;
            }
            catch
            {
                return true;
            }
        }

        private static string[] ParseExclusions(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new string[0];

            return raw
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
        }

        private static bool IsExcludedByName(Element element, string[] fragments)
        {
            if (fragments == null || fragments.Length == 0)
                return false;

            string name;
            try
            {
                name = element?.Name ?? string.Empty;
            }
            catch
            {
                return false;
            }

            return fragments.Any(f => name.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        /// <summary>
        /// Identities of every element an existing dimension in this view already references, so a
        /// second run does not stack another identical set on top.
        /// </summary>
        private static HashSet<string> BuildDimensionedKeySet(Document doc, View view)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            IList<Element> dimensions;
            try
            {
                // Category filter, not OfClass(typeof(Dimension)): Revit 2025+ returns linear
                // dimensions as the LinearDimension subclass, which an exact-type filter misses.
                dimensions = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Dimensions)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }
            catch
            {
                return keys;
            }

            foreach (Dimension dimension in dimensions.OfType<Dimension>())
            {
                ReferenceArray references;
                try
                {
                    references = dimension.References;
                }
                catch
                {
                    continue;
                }

                if (references == null)
                    continue;

                ReferenceArrayIterator iterator = references.ForwardIterator();
                while (iterator.MoveNext())
                {
                    if (!(iterator.Current is Reference reference))
                        continue;

                    string key = DimensionSource.GetReferenceTargetKey(reference);
                    if (!string.IsNullOrEmpty(key))
                        keys.Add(key);
                }
            }

            return keys;
        }

        private static DimensionType ResolveType(Document doc, string typeName, DimensionRunReport report)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            DimensionType resolved = DimensionFactory.ResolveDimensionType(doc, typeName);
            if (resolved == null)
            {
                report.AddNote("Dimension type \"" + typeName +
                               "\" is not in this project - the default type was used instead.");
            }

            return resolved;
        }

        /// <summary>Defensive check for area plans without relying on the enum value existing.</summary>
        private static bool IsAreaPlan(ViewType viewType)
        {
            try
            {
                return viewType.ToString() == "AreaPlan";
            }
            catch
            {
                return false;
            }
        }

        // -----------------------------------------------------------------------------------------
        // View space
        // -----------------------------------------------------------------------------------------

        private enum ViewAxis
        {
            /// <summary>Datums vary across the screen; rows run horizontally above or below.</summary>
            AlongX,

            /// <summary>Datums vary up the screen; rows run vertically left or right.</summary>
            AlongY
        }

        private enum RowSide
        {
            /// <summary>Above for horizontal rows, left for vertical rows.</summary>
            Primary,

            /// <summary>Below for horizontal rows, right for vertical rows.</summary>
            Secondary
        }

        private sealed class DatumEntry
        {
            public Element Datum { get; set; }
            public Reference HostReference { get; set; }

            /// <summary>An existing dimension in this view already references this datum.</summary>
            public bool AlreadyDimensioned { get; set; }

            public ViewAxis Axis { get; set; }

            /// <summary>Position along the measuring direction, in view coordinates.</summary>
            public double Coord { get; set; }

            public double MinExtent { get; set; }
            public double MaxExtent { get; set; }
            public double MinExtentY { get; set; }
            public double MaxExtentY { get; set; }
            public string SourceName { get; set; }

            /// <summary>
            /// False for levels: their projected horizontal position is arbitrary and must not be used
            /// to size the frame the dimension rows are placed around.
            /// </summary>
            public bool ContributesHorizontalExtent { get; set; } = true;
        }

        private struct ViewCoords
        {
            public double X;
            public double Y;

            public static ViewCoords From(View view, XYZ modelPoint)
            {
                XYZ delta = modelPoint - view.Origin;
                return new ViewCoords
                {
                    X = delta.DotProduct(view.RightDirection.Normalize()),
                    Y = delta.DotProduct(view.UpDirection.Normalize())
                };
            }
        }

        /// <summary>
        /// The rectangle the rows are placed around, in view coordinates. Taken from the crop box when
        /// it is on, and from the datums themselves when it is not - so the tool still runs uncropped.
        /// </summary>
        private sealed class ViewFrame
        {
            private XYZ _origin;
            private XYZ _right;
            private XYZ _up;

            public double MinX { get; private set; }
            public double MaxX { get; private set; }
            public double MinY { get; private set; }
            public double MaxY { get; private set; }

            /// <summary>
            /// The crop rectangle alone, in view coordinates. Empty when the view has no active crop.
            /// Used to cull linked datums, which Revit cannot scope to a view for us.
            /// </summary>
            public static ViewFrame BuildFromCropOnly(View view)
            {
                ViewFrame frame = Create(view);

                bool cropActive;
                try
                {
                    cropActive = view.CropBoxActive;
                }
                catch
                {
                    cropActive = false;
                }

                if (!cropActive)
                    return frame;

                try
                {
                    BoundingBoxXYZ crop = view.CropBox;
                    if (crop != null)
                        frame.AbsorbBox(view, crop);
                }
                catch
                {
                    // Leaves the frame empty, which imposes no limit.
                }

                return frame;
            }

            private static ViewFrame Create(View view)
            {
                return new ViewFrame
                {
                    _origin = view.Origin,
                    _right = view.RightDirection.Normalize(),
                    _up = view.UpDirection.Normalize(),
                    MinX = double.MaxValue,
                    MaxX = double.MinValue,
                    MinY = double.MaxValue,
                    MaxY = double.MinValue
                };
            }

            /// <summary>
            /// Builds the frame from the crop box when the crop is on, and from the datums' own extents
            /// otherwise - which is what lets the tool run in an uncropped view instead of refusing.
            /// </summary>
            public static ViewFrame Build(View view, IEnumerable<DatumEntry> entries)
            {
                ViewFrame frame = new ViewFrame
                {
                    _origin = view.Origin,
                    _right = view.RightDirection.Normalize(),
                    _up = view.UpDirection.Normalize(),
                    MinX = double.MaxValue,
                    MaxX = double.MinValue,
                    MinY = double.MaxValue,
                    MaxY = double.MinValue
                };

                bool cropActive;
                try
                {
                    cropActive = view.CropBoxActive;
                }
                catch
                {
                    cropActive = false;
                }

                if (cropActive)
                {
                    BoundingBoxXYZ crop = null;
                    try
                    {
                        crop = view.CropBox;
                    }
                    catch
                    {
                        crop = null;
                    }

                    if (crop != null)
                    {
                        frame.AbsorbBox(view, crop);
                        if (!frame.IsEmpty)
                            return frame;
                    }
                }

                List<DatumEntry> list = (entries ?? Enumerable.Empty<DatumEntry>()).ToList();

                // Horizontal extent comes only from datums whose horizontal position is meaningful
                // (grids). Vertical extent comes from everything, so levels still set the row's height.
                foreach (DatumEntry entry in list.Where(e => e.ContributesHorizontalExtent))
                {
                    frame.AbsorbX(entry.MinExtent);
                    frame.AbsorbX(entry.MaxExtent);
                }

                foreach (DatumEntry entry in list)
                {
                    frame.AbsorbY(entry.MinExtentY);
                    frame.AbsorbY(entry.MaxExtentY);
                }

                // A levels-only uncropped section has no grid to give a horizontal extent. Fall back to
                // the levels' own projected position so the row still lands beside them.
                if (frame.MinX > frame.MaxX)
                {
                    foreach (DatumEntry entry in list)
                    {
                        frame.AbsorbX(entry.MinExtent);
                        frame.AbsorbX(entry.MaxExtent);
                    }
                }

                if (frame.IsEmpty)
                {
                    // Nothing usable at all - fall back to a frame at the view origin so a row can
                    // still be placed rather than the run failing outright.
                    frame.Absorb(0.0, 0.0);
                }

                return frame;
            }

            private bool AbsorbBox(View view, BoundingBoxXYZ box)
            {
                Transform boxTransform = box.Transform ?? Transform.Identity;
                double[] xs = { box.Min.X, box.Max.X };
                double[] ys = { box.Min.Y, box.Max.Y };
                double[] zs = { box.Min.Z, box.Max.Z };

                foreach (double x in xs)
                {
                    foreach (double y in ys)
                    {
                        foreach (double z in zs)
                        {
                            ViewCoords corner = ViewCoords.From(view, boxTransform.OfPoint(new XYZ(x, y, z)));
                            Absorb(corner.X, corner.Y);
                        }
                    }
                }

                return MinX <= MaxX && MinY <= MaxY;
            }

            public void Absorb(double x, double y)
            {
                AbsorbX(x);
                AbsorbY(y);
            }

            public void AbsorbX(double x)
            {
                if (x < MinX) MinX = x;
                if (x > MaxX) MaxX = x;
            }

            public void AbsorbY(double y)
            {
                if (y < MinY) MinY = y;
                if (y > MaxY) MaxY = y;
            }

            public bool IsEmpty
            {
                get { return MinX > MaxX || MinY > MaxY; }
            }

            public XYZ ToModel(double x, double y)
            {
                return _origin + (_right * x) + (_up * y);
            }
        }
    }
}
