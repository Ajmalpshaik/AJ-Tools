#region Metadata
/*
 * Tool Name     : Tag Clash Engine
 * File Name     : TagClashEngine.cs
 * Purpose       : The ONE tag clash detection engine for AJ Tools. Works the way Ajmal specified:
 *                 place the tags first, then find the clashes, then fix only the clashing few -
 *                 instead of asking "does this clash?" before every single placement.
 *
 *                 Why that matters: Smart MEP Tag's original approach tries up to 24 positions per
 *                 element (4 sides x 3 points along the run x 2 leader lengths) and clash-tests every
 *                 one of them. For 10,000 tags that is roughly 240,000 clash questions asked before a
 *                 single tag exists. Placing first and fixing after means the expensive work only ever
 *                 runs on the handful that actually collide.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-16
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.SmartTag (AnnotationBox, AnnotationSpatialIndex,
 *                 SmartMepTagService), AJTools.Services.LeaderLogic (LeaderLogicService),
 *                 AJTools.Utils (TagCompat, TagClashSettings, Constants)
 *
 * Input         : A view full of tags, plus the shared TagClashSettingsState.
 * Output        : Tags moved apart, and the element ids of any that could not be separated.
 *
 * Notes         :
 * - KEPT FROM THE OLD ENGINE (these were good and are reused deliberately):
 *     * AnnotationBox            - the flat rectangle with Overlaps / DistanceTo / OverlapArea.
 *     * AnnotationSpatialIndex   - the grid. This matters MORE here than it did before: comparing
 *                                  10,000 tags to each other without it is ~50 million comparisons.
 *     * The tuned tolerances     - 1.5mm hard clash and 5mm minimum gap, on the printed sheet.
 *     * The rotated-bounding-box and tag-text-box techniques (now in TagViewGeometry).
 * - THE RULE FOR WHO MOVES (Ajmal's): when two tags clash, the one with the SHORTER leader - the one
 *   sitting closer to its own element - stays put. The stretched one moves. Ties break on element id
 *   so a re-run gives the same answer every time.
 * - ANTI-PING-PONG: a tag that wins a contest is frozen for the rest of the run and never moves
 *   again, and a moved tag may never travel further than the drift limit from where it started. Both
 *   guards exist because A-pushes-B-pushes-A would otherwise loop forever.
 * - Static annotations (text notes, dimensions, and the other annotation categories) are obstacles,
 *   never movers - a tag clashing with one of those always loses.
 * - Tag boxes are SHIFTED by the move delta rather than re-read from Revit after each move. Re-reading
 *   forces a regeneration per tag, which is exactly the cost this design exists to avoid; the text
 *   does not change size when the tag moves, so shifting is accurate.
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Services.LeaderLogic;
using AJTools.Services.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.TagClash
{
    /// <summary>
    /// What a clash-fixing run did, for the end-of-run report.
    /// </summary>
    internal sealed class TagClashRunResult
    {
        public TagClashRunResult()
        {
            UnfixedTagIds = new List<ElementId>();
        }

        /// <summary>Tags examined in the view.</summary>
        public int TotalTags { get; set; }

        /// <summary>Tags involved in at least one clash before anything was moved.</summary>
        public int ClashingAtStart { get; set; }

        /// <summary>Tags that were moved and ended up clear.</summary>
        public int Fixed { get; set; }

        /// <summary>Tags still clashing when the passes ran out.</summary>
        public int StillClashing { get; set; }

        /// <summary>How many fix rounds actually ran (stops early once the view is clean).</summary>
        public int PassesUsed { get; set; }

        /// <summary>Tags that clash but are pinned, so they were left alone.</summary>
        public int SkippedPinned { get; set; }

        /// <summary>Times a move was refused because it would exceed the drift limit.</summary>
        public int BlockedByDriftLimit { get; set; }

        /// <summary>Total moves applied across every pass.</summary>
        public int MovesApplied { get; set; }

        /// <summary>Element ids of the tags still clashing at the end - these get marked.</summary>
        public List<ElementId> UnfixedTagIds { get; private set; }
    }

    /// <summary>
    /// One tag the engine may move, with everything it needs cached so the loop never
    /// re-queries Revit.
    /// </summary>
    internal sealed class TagClashItem
    {
        /// <summary>The tag element.</summary>
        public Element Tag { get; set; }

        /// <summary>The tag's element id.</summary>
        public ElementId Id { get; set; }

        /// <summary>Current text box in view-plane coordinates.</summary>
        public AnnotationBox Box { get; set; }

        /// <summary>Current tag head position, model coordinates.</summary>
        public XYZ Head { get; set; }

        /// <summary>Where the head started, so drift can be measured.</summary>
        public XYZ OriginalHead { get; set; }

        /// <summary>
        /// The point on the model this tag belongs to - its leader end where readable, otherwise the
        /// midpoint of the tagged element. Used to decide who has the shorter leader.
        /// </summary>
        public XYZ Anchor { get; set; }

        /// <summary>The tagged element, when it could be resolved. Used for sliding along a run.</summary>
        public Element HostElement { get; set; }

        /// <summary>False for pinned tags - they are obstacles, not movers.</summary>
        public bool CanMove { get; set; }

        /// <summary>True once this tag has won a contest; it is never moved again this run.</summary>
        public bool Frozen { get; set; }
    }

    /// <summary>
    /// Finds and fixes tag-on-tag and tag-on-annotation clashes in a view.
    /// </summary>
    internal static class TagClashEngine
    {
        private const double SpatialIndexCellPaperMm = 20.0;

        // ═══════════════════════════════════════════════════════════════
        // JOB 1 — BUILD THE PICTURE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Collects every movable tag in the view, with its text box, head, anchor and host.
        /// </summary>
        internal static List<TagClashItem> CollectMovableTags(Document doc, View view, XYZ viewRight, XYZ viewUp)
        {
            var items = new List<TagClashItem>();
            if (doc == null || view == null)
                return items;

            IList<Element> tags;
            try
            {
                tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType()
                    .ToElements();
            }
            catch (Exception)
            {
                return items;
            }

            foreach (Element element in tags)
            {
                IndependentTag tag = element as IndependentTag;
                if (tag == null || !tag.IsValidObject)
                    continue;

                AnnotationBox box = TagViewGeometry.GetTagTextBox(tag, view, viewRight, viewUp);
                if (box == null)
                    continue;

                XYZ head = TagViewGeometry.GetTagHeadPosition(tag);
                if (head == null)
                    continue;

                bool pinned;
                try
                {
                    pinned = tag.Pinned;
                }
                catch (Exception)
                {
                    pinned = true;
                }

                Element host = TryGetHostElement(doc, tag);

                items.Add(new TagClashItem
                {
                    Tag = tag,
                    Id = tag.Id,
                    Box = box,
                    Head = head,
                    OriginalHead = head,
                    Anchor = ResolveAnchor(tag, host, view, head),
                    HostElement = host,
                    CanMove = !pinned,
                    Frozen = false
                });
            }

            // Stable order so a re-run produces the same result.
            items.Sort((a, b) => a.Id.IntValue().CompareTo(b.Id.IntValue()));
            return items;
        }

        /// <summary>
        /// Collects the annotations a tag must avoid but can never push out of the way: text notes,
        /// dimensions, and the other annotation categories the old engine was blind to.
        /// </summary>
        internal static List<AnnotationBox> CollectStaticObstacles(Document doc, View view, XYZ viewRight, XYZ viewUp)
        {
            var boxes = new List<AnnotationBox>();
            if (doc == null || view == null)
                return boxes;

            AddByClass(doc, view, typeof(TextNote), boxes, viewRight, viewUp);

            // Category-based, not OfClass(typeof(Dimension)): Revit 2025+ returns linear dimensions
            // as the LinearDimension subclass, which an exact-type OfClass filter misses.
            BuiltInCategory[] obstacleCategories = new[]
            {
                BuiltInCategory.OST_Dimensions,
                BuiltInCategory.OST_DetailComponents,
                BuiltInCategory.OST_Lines,
                BuiltInCategory.OST_GenericAnnotation,
                BuiltInCategory.OST_KeynoteTags,
                BuiltInCategory.OST_SpotElevations,
                BuiltInCategory.OST_SpotCoordinates,
                BuiltInCategory.OST_RevisionClouds,
                BuiltInCategory.OST_RevisionCloudTags
            };

            foreach (BuiltInCategory category in obstacleCategories)
                AddByCategory(doc, view, category, boxes, viewRight, viewUp);

            return boxes;
        }

        private static void AddByClass(
            Document doc, View view, Type type, List<AnnotationBox> boxes, XYZ viewRight, XYZ viewUp)
        {
            try
            {
                var collector = new FilteredElementCollector(doc, view.Id)
                    .OfClass(type)
                    .WhereElementIsNotElementType();

                foreach (Element element in collector)
                    AddBox(element, view, boxes, viewRight, viewUp);
            }
            catch (Exception)
            {
                // A category or class missing from this project is not an error - skip it.
            }
        }

        private static void AddByCategory(
            Document doc, View view, BuiltInCategory category, List<AnnotationBox> boxes, XYZ viewRight, XYZ viewUp)
        {
            try
            {
                var collector = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();

                foreach (Element element in collector)
                    AddBox(element, view, boxes, viewRight, viewUp);
            }
            catch (Exception)
            {
                // A category or class missing from this project is not an error - skip it.
            }
        }

        private static void AddBox(
            Element element, View view, List<AnnotationBox> boxes, XYZ viewRight, XYZ viewUp)
        {
            AnnotationBox box = TagViewGeometry.ToViewBox(
                TagViewGeometry.GetBoundingBox(element, view), viewRight, viewUp);
            if (box != null)
                boxes.Add(box);
        }

        // ═══════════════════════════════════════════════════════════════
        // JOB 2 — IS THIS SPOT CLEAR?
        // A single question any tool can ask before placing a tag.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// True when the given rectangle is clear of everything in the index. <paramref name="ignore"/>
        /// is the moving tag's own current box, which must not count as a clash against itself.
        /// </summary>
        internal static bool IsSpotClear(
            AnnotationBox proposed,
            AnnotationSpatialIndex index,
            double toleranceFeet,
            double minGapFeet,
            AnnotationBox ignore)
        {
            if (proposed == null || index == null)
                return true;

            foreach (AnnotationBox other in index.Query(proposed.Inflated(minGapFeet)))
            {
                if (ReferenceEquals(other, proposed) || ReferenceEquals(other, ignore))
                    continue;

                if (IsClashing(proposed, other, toleranceFeet, minGapFeet))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Two rectangles clash when they overlap by more than the tolerance, or when they are apart
        /// but closer than the minimum gap. Both measured on the printed sheet.
        /// </summary>
        internal static bool IsClashing(
            AnnotationBox a, AnnotationBox b, double toleranceFeet, double minGapFeet)
        {
            if (a == null || b == null)
                return false;

            double gap = a.DistanceTo(b);
            if (gap > Constants.ZERO_LENGTH_TOLERANCE)
                return gap < minGapFeet;

            // Touching or overlapping - ignore slivers below the tolerance.
            return a.OverlapArea(b) > (toleranceFeet * toleranceFeet);
        }

        // ═══════════════════════════════════════════════════════════════
        // JOBS 3-4 — FIND ALL CLASHES, THEN FIX THEM, PASS BY PASS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs the whole find-and-fix loop over a view. The caller owns the transaction.
        /// </summary>
        internal static TagClashRunResult Run(
            Document doc,
            View view,
            TagClashSettingsState settings,
            IList<TagClashItem> items,
            IList<AnnotationBox> staticObstacles)
        {
            var result = new TagClashRunResult();
            if (doc == null || view == null || items == null)
                return result;

            if (settings == null)
                settings = TagClashSettings.CreateDefaults();

            XYZ viewRight = TagViewGeometry.GetViewRight(view);
            XYZ viewUp = TagViewGeometry.GetViewUp(view);
            int viewScale = TagViewGeometry.GetViewScale(view);

            double toleranceFeet = TagViewGeometry.PaperMmToFeet(settings.ClashToleranceMm, viewScale);
            double minGapFeet = TagViewGeometry.PaperMmToFeet(settings.MinGapMm, viewScale);
            double maxDriftFeet = TagViewGeometry.PaperMmToFeet(settings.MaxDriftMm, viewScale);
            double cellSizeFeet = TagViewGeometry.PaperMmToFeet(SpatialIndexCellPaperMm, viewScale);

            var leaderLogic = new LeaderLogicService(view);
            result.TotalTags = items.Count;

            bool firstPass = true;
            int passes = TagClashSettings.ClampPasses(settings.FixPasses);

            for (int pass = 0; pass < passes; pass++)
            {
                result.PassesUsed = pass + 1;

                Dictionary<int, List<TagClashItem>> tagPartners;
                HashSet<int> blockedByObstacle;
                List<TagClashItem> offenders = FindClashes(
                    items, staticObstacles, cellSizeFeet, toleranceFeet, minGapFeet,
                    out tagPartners, out blockedByObstacle);

                if (firstPass)
                {
                    result.ClashingAtStart = offenders.Count;
                    result.SkippedPinned = CountPinned(offenders);
                    firstPass = false;
                }

                if (offenders.Count == 0)
                    break;

                bool movedAnything = false;

                // ONE avoidance index per pass, not one per move: rebuilding it for every tag would
                // cost O(tags) per move and undo the whole point of this design. Each successful move
                // adds the tag's NEW box straight into this index, so nothing later in the same pass
                // can land on top of it. The tag's OLD box stays in the index and is not removed
                // (AnnotationSpatialIndex has no remove) - that only makes the vacated spot look busy
                // for the rest of this pass, which is the safe direction to be wrong in, and the next
                // pass rebuilds from scratch.
                AnnotationSpatialIndex avoidIndex = BuildAvoidanceIndex(
                    items, staticObstacles, cellSizeFeet);

                foreach (TagClashItem offender in offenders)
                {
                    if (!offender.CanMove || offender.Frozen)
                        continue;

                    List<TagClashItem> partners;
                    if (!tagPartners.TryGetValue(offender.Id.IntValue(), out partners))
                        partners = new List<TagClashItem>();

                    bool hitsObstacle = blockedByObstacle.Contains(offender.Id.IntValue());

                    // Ajmal's rule: the tag closest to its own element stays. A tag clashing with a
                    // static annotation always loses, because the annotation cannot move.
                    if (!hitsObstacle && IsClosestToItsOwnElement(offender, partners, viewRight, viewUp))
                    {
                        offender.Frozen = true;
                        continue;
                    }

                    bool moved = TryMoveClear(
                        doc, leaderLogic, offender, avoidIndex,
                        toleranceFeet, minGapFeet, maxDriftFeet,
                        settings.FullSearch, viewRight, viewUp, result);

                    if (moved)
                    {
                        movedAnything = true;
                        result.MovesApplied++;
                    }
                }

                // Nothing could be improved this round - more passes will not help.
                if (!movedAnything)
                    break;
            }

            // Final read: who is still clashing?
            List<TagClashItem> remaining = FindClashes(
                items, staticObstacles, cellSizeFeet, toleranceFeet, minGapFeet,
                out _, out _);

            foreach (TagClashItem item in remaining)
                result.UnfixedTagIds.Add(item.Id);

            result.StillClashing = remaining.Count;
            result.Fixed = result.ClashingAtStart - result.StillClashing;
            if (result.Fixed < 0)
                result.Fixed = 0;

            return result;
        }

        /// <summary>
        /// One sweep of the whole view: which tags are involved in a clash, who with, and which of
        /// them are stuck against something that cannot move.
        /// </summary>
        private static List<TagClashItem> FindClashes(
            IList<TagClashItem> items,
            IList<AnnotationBox> staticObstacles,
            double cellSizeFeet,
            double toleranceFeet,
            double minGapFeet,
            out Dictionary<int, List<TagClashItem>> tagPartners,
            out HashSet<int> blockedByObstacle)
        {
            tagPartners = new Dictionary<int, List<TagClashItem>>();
            blockedByObstacle = new HashSet<int>();
            var offenders = new List<TagClashItem>();

            var index = new AnnotationSpatialIndex(cellSizeFeet);
            var ownerByBox = new Dictionary<AnnotationBox, TagClashItem>();

            foreach (TagClashItem item in items)
            {
                if (item == null || item.Box == null)
                    continue;

                ownerByBox[item.Box] = item;
                index.Add(item.Box);
            }

            var obstacleBoxes = new HashSet<AnnotationBox>();
            if (staticObstacles != null)
            {
                foreach (AnnotationBox box in staticObstacles)
                {
                    if (box == null)
                        continue;

                    obstacleBoxes.Add(box);
                    index.Add(box);
                }
            }

            foreach (TagClashItem item in items)
            {
                if (item == null || item.Box == null)
                    continue;

                var partners = new List<TagClashItem>();
                bool hitsObstacle = false;

                foreach (AnnotationBox other in index.Query(item.Box.Inflated(minGapFeet)))
                {
                    if (ReferenceEquals(other, item.Box))
                        continue;

                    if (!IsClashing(item.Box, other, toleranceFeet, minGapFeet))
                        continue;

                    TagClashItem owner;
                    if (ownerByBox.TryGetValue(other, out owner) && owner != null)
                    {
                        partners.Add(owner);
                        continue;
                    }

                    if (obstacleBoxes.Contains(other))
                        hitsObstacle = true;
                }

                if (partners.Count == 0 && !hitsObstacle)
                    continue;

                offenders.Add(item);
                tagPartners[item.Id.IntValue()] = partners;
                if (hitsObstacle)
                    blockedByObstacle.Add(item.Id.IntValue());
            }

            return offenders;
        }

        /// <summary>
        /// True when this tag sits closer to its own element than every tag it clashes with, so it is
        /// the one that keeps its place. Ties break on element id so re-runs match.
        /// </summary>
        private static bool IsClosestToItsOwnElement(
            TagClashItem item, IList<TagClashItem> partners, XYZ viewRight, XYZ viewUp)
        {
            if (item == null || partners == null || partners.Count == 0)
                return true;

            double own = LeaderLength(item, viewRight, viewUp);

            foreach (TagClashItem partner in partners)
            {
                if (partner == null)
                    continue;

                double other = LeaderLength(partner, viewRight, viewUp);

                if (other < own - Constants.ZERO_LENGTH_TOLERANCE)
                    return false;

                if (Math.Abs(other - own) <= Constants.ZERO_LENGTH_TOLERANCE
                    && partner.Id.IntValue() < item.Id.IntValue())
                {
                    return false;
                }
            }

            return true;
        }

        private static double LeaderLength(TagClashItem item, XYZ viewRight, XYZ viewUp)
        {
            if (item == null || item.Head == null || item.Anchor == null)
                return double.MaxValue;

            return TagViewGeometry.DistanceInView(item.Head, item.Anchor, viewRight, viewUp);
        }

        private static int CountPinned(IList<TagClashItem> offenders)
        {
            int count = 0;
            foreach (TagClashItem offender in offenders)
            {
                if (offender != null && !offender.CanMove)
                    count++;
            }
            return count;
        }

        // ═══════════════════════════════════════════════════════════════
        // MOVING A TAG CLEAR
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tries to find and apply a clear position for one tag. With FullSearch on it tries the
        /// other side of the host and sliding along the run before falling back to stepping up and
        /// down - the same choices the old engine had, but paid for only on the few tags that clash.
        /// </summary>
        private static bool TryMoveClear(
            Document doc,
            LeaderLogicService leaderLogic,
            TagClashItem item,
            AnnotationSpatialIndex avoidIndex,
            double toleranceFeet,
            double minGapFeet,
            double maxDriftFeet,
            bool fullSearch,
            XYZ viewRight,
            XYZ viewUp,
            TagClashRunResult result)
        {
            if (item == null || item.Box == null || item.Head == null)
                return false;

            // Shortest-way-out moves first, measured against what this tag is ACTUALLY hitting, then
            // the fixed-size steps. Both go into one pool - the loop below still picks the smallest
            // move that ends up genuinely clear, so a measured escape only wins when it really works.
            List<XYZ> offsets = BuildOverlapEscapeOffsets(
                item, avoidIndex, toleranceFeet, minGapFeet, viewRight, viewUp);

            offsets.AddRange(BuildCandidateOffsets(item, fullSearch, minGapFeet, viewRight, viewUp));

            XYZ bestOffset = null;
            double bestDistance = double.MaxValue;

            foreach (XYZ offset in offsets)
            {
                if (offset == null)
                    continue;

                double moveLength = offset.GetLength();
                if (moveLength <= Constants.ZERO_LENGTH_TOLERANCE)
                    continue;

                XYZ proposedHead = item.Head.Add(offset);

                // Never let a tag wander further than the drift limit from where it started.
                double drift = TagViewGeometry.DistanceInView(
                    proposedHead, item.OriginalHead, viewRight, viewUp);
                if (drift > maxDriftFeet)
                {
                    result.BlockedByDriftLimit++;
                    continue;
                }

                double deltaRight = offset.DotProduct(viewRight);
                double deltaUp = offset.DotProduct(viewUp);
                AnnotationBox proposedBox = TagViewGeometry.ShiftBox(item.Box, deltaRight, deltaUp);

                if (!IsSpotClear(proposedBox, avoidIndex, toleranceFeet, minGapFeet, item.Box))
                    continue;

                // Prefer the smallest move that works.
                if (moveLength < bestDistance)
                {
                    bestDistance = moveLength;
                    bestOffset = offset;
                }
            }

            if (bestOffset == null)
                return false;

            if (!ApplyMove(doc, leaderLogic, item, bestOffset, viewRight, viewUp))
                return false;

            // Publish the new position so nothing later in this pass lands on top of it.
            if (avoidIndex != null && item.Box != null)
                avoidIndex.Add(item.Box);

            return true;
        }

        /// <summary>
        /// Builds the shortest moves that would clear this tag of whatever it is actually touching -
        /// one horizontal and one vertical escape per offending neighbour, each sized to the real
        /// overlap rather than to a fixed step.
        /// </summary>
        /// <remarks>
        /// Why this exists (added v1.49.6): every other candidate is a fixed quantum - a whole tag
        /// height up or down, a whole tag width sideways. So two tags overlapping by a hair were still
        /// shoved a full tag apart, which looks wrong on a drawing and burns the drift allowance for
        /// nothing - and a tag that runs out of drift is left clashing and marked instead of fixed.
        /// Measuring the overlap and moving exactly that far plus the gap is the idea taken from the
        /// AJ AI Brain's push-apart pass (knowledge/live-model/tagging.md), which is the one thing it
        /// did better than this engine.
        ///
        /// Deliberately NOT the Brain's version of the rest: it splits the move 50/50 across BOTH
        /// tags, which this engine must not do - who moves is already decided by leader length, and a
        /// frozen winner may never move again. These are candidates for the offender only.
        ///
        /// The clearance added is minGap + tolerance, not just minGap, because a pair left exactly on
        /// the gap still satisfies IsClashing's "closer than the minimum gap" test on the next pass -
        /// landing exactly on the boundary would make the tag oscillate instead of settling.
        /// </remarks>
        private static List<XYZ> BuildOverlapEscapeOffsets(
            TagClashItem item,
            AnnotationSpatialIndex avoidIndex,
            double toleranceFeet,
            double minGapFeet,
            XYZ viewRight,
            XYZ viewUp)
        {
            var offsets = new List<XYZ>();

            if (item == null || item.Box == null || avoidIndex == null)
                return offsets;

            double clearance = minGapFeet + toleranceFeet;

            foreach (AnnotationBox other in avoidIndex.Query(item.Box.Inflated(minGapFeet)))
            {
                if (other == null || ReferenceEquals(other, item.Box))
                    continue;

                // Only the things genuinely in the way get a vote - same test the fixer uses, so a
                // neighbour that is merely nearby never drags the tag around.
                if (!IsClashing(item.Box, other, toleranceFeet, minGapFeet))
                    continue;

                // Four ways past this one neighbour - out past each of its edges. Two are positive,
                // two negative; keeping the smaller magnitude on each axis is the shortest escape.
                double outRight = (other.MaxX + clearance) - item.Box.MinX;
                double outLeft = (other.MinX - clearance) - item.Box.MaxX;
                double outUp = (other.MaxY + clearance) - item.Box.MinY;
                double outDown = (other.MinY - clearance) - item.Box.MaxY;

                double shortestX = Math.Abs(outRight) <= Math.Abs(outLeft) ? outRight : outLeft;
                double shortestY = Math.Abs(outUp) <= Math.Abs(outDown) ? outUp : outDown;

                if (Math.Abs(shortestX) > Constants.ZERO_LENGTH_TOLERANCE)
                    offsets.Add(viewRight.Multiply(shortestX));

                if (Math.Abs(shortestY) > Constants.ZERO_LENGTH_TOLERANCE)
                    offsets.Add(viewUp.Multiply(shortestY));
            }

            return offsets;
        }

        /// <summary>
        /// Builds the list of moves worth trying, nearest first. Vertical steps come from the tag's
        /// own height so a tag always clears by at least its own size plus the minimum gap.
        /// </summary>
        private static List<XYZ> BuildCandidateOffsets(
            TagClashItem item, bool fullSearch, double minGapFeet, XYZ viewRight, XYZ viewUp)
        {
            var offsets = new List<XYZ>();

            double tagHeight = item.Box.MaxY - item.Box.MinY;
            if (tagHeight <= Constants.ZERO_LENGTH_TOLERANCE)
                tagHeight = minGapFeet;

            double step = tagHeight + minGapFeet;

            // Up and down first - this is the plain behaviour Ajmal described.
            for (int multiple = 1; multiple <= 4; multiple++)
            {
                offsets.Add(viewUp.Multiply(step * multiple));
                offsets.Add(viewUp.Multiply(-step * multiple));
            }

            if (!fullSearch)
                return offsets;

            // Mirror to the other side of the element: keep the horizontal offset from the anchor,
            // flip the vertical one. This is how a modeller would move a tag that will not fit above.
            if (item.Anchor != null && item.Head != null)
            {
                double headUp = item.Head.DotProduct(viewUp);
                double anchorUp = item.Anchor.DotProduct(viewUp);
                double verticalGap = headUp - anchorUp;
                if (Math.Abs(verticalGap) > Constants.ZERO_LENGTH_TOLERANCE)
                    offsets.Add(viewUp.Multiply(-2.0 * verticalGap));
            }

            // Slide along the run, keeping the same offset from it.
            LocationCurve locationCurve = item.HostElement != null
                ? item.HostElement.Location as LocationCurve
                : null;

            if (locationCurve != null && locationCurve.Curve != null && item.Anchor != null)
            {
                double[] parameters = new[] { 0.25, 0.75, 0.1, 0.9 };
                foreach (double parameter in parameters)
                {
                    try
                    {
                        XYZ slidAnchor = locationCurve.Curve.Evaluate(parameter, true);
                        if (slidAnchor == null)
                            continue;

                        XYZ slide = slidAnchor.Subtract(item.Anchor);
                        double slideRight = slide.DotProduct(viewRight);
                        if (Math.Abs(slideRight) <= Constants.ZERO_LENGTH_TOLERANCE)
                            continue;

                        offsets.Add(viewRight.Multiply(slideRight));
                    }
                    catch (Exception)
                    {
                        // A curve that will not evaluate simply contributes no candidate.
                    }
                }
            }

            // Sideways nudges as a last resort.
            double tagWidth = item.Box.MaxX - item.Box.MinX;
            if (tagWidth <= Constants.ZERO_LENGTH_TOLERANCE)
                tagWidth = minGapFeet;

            offsets.Add(viewRight.Multiply(tagWidth + minGapFeet));
            offsets.Add(viewRight.Multiply(-(tagWidth + minGapFeet)));

            return offsets;
        }

        /// <summary>
        /// Everything a tag must avoid: every tag's current box plus the static obstacles. Built once
        /// per pass; the moving tag excludes its own box at query time via IsSpotClear's ignore.
        /// </summary>
        private static AnnotationSpatialIndex BuildAvoidanceIndex(
            IList<TagClashItem> allItems,
            IList<AnnotationBox> staticObstacles,
            double cellSizeFeet)
        {
            var index = new AnnotationSpatialIndex(cellSizeFeet);

            if (allItems != null)
            {
                foreach (TagClashItem other in allItems)
                {
                    if (other != null && other.Box != null)
                        index.Add(other.Box);
                }
            }

            if (staticObstacles != null)
            {
                foreach (AnnotationBox box in staticObstacles)
                {
                    if (box != null)
                        index.Add(box);
                }
            }

            return index;
        }

        /// <summary>
        /// Applies the move to the real tag and re-points its leader elbow, then updates the cached
        /// head and box so the next pass sees the new position without re-reading Revit.
        /// </summary>
        private static bool ApplyMove(
            Document doc,
            LeaderLogicService leaderLogic,
            TagClashItem item,
            XYZ offset,
            XYZ viewRight,
            XYZ viewUp)
        {
            try
            {
                ElementTransformUtils.MoveElement(doc, item.Id, offset);
            }
            catch (Exception)
            {
                return false;
            }

            double deltaRight = offset.DotProduct(viewRight);
            double deltaUp = offset.DotProduct(viewUp);

            item.Head = item.Head.Add(offset);
            item.Box = TagViewGeometry.ShiftBox(item.Box, deltaRight, deltaUp);

            RepairLeader(item, leaderLogic);
            return true;
        }

        /// <summary>
        /// Re-computes the L-shaped elbow after a move, using the shared leader logic. Failure here is
        /// not fatal - the tag has still been separated, its leader is just not re-shaped.
        /// </summary>
        private static void RepairLeader(TagClashItem item, LeaderLogicService leaderLogic)
        {
            IndependentTag tag = item.Tag as IndependentTag;
            if (tag == null || leaderLogic == null)
                return;

            try
            {
                if (!tag.HasLeader)
                    return;
            }
            catch (Exception)
            {
                return;
            }

            XYZ head;
            try
            {
                head = tag.TagHeadPosition;
            }
            catch (Exception)
            {
                return;
            }

            if (head == null)
                return;

            XYZ leaderEnd = LeaderLogicService.GetL1(tag);
            if (leaderEnd == null)
                return;

            XYZ elbow = leaderLogic.ComputeElbow(head, leaderEnd);
            if (elbow == null)
                return;

            LeaderLogicService.TrySetLeaderElbow(tag, elbow);
        }

        // ═══════════════════════════════════════════════════════════════
        // ANCHOR / HOST RESOLUTION
        // ═══════════════════════════════════════════════════════════════

        private static Element TryGetHostElement(Document doc, IndependentTag tag)
        {
            if (doc == null || tag == null)
                return null;

            try
            {
                ElementId hostId = TagCompat.GetTaggedLocalElementId(tag);
                if (hostId == null || hostId == ElementId.InvalidElementId)
                    return null;

                return doc.GetElement(hostId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The point this tag belongs to: its leader end where Revit will give it, otherwise the
        /// midpoint of the tagged element, otherwise the head itself (leader length zero).
        /// </summary>
        private static XYZ ResolveAnchor(IndependentTag tag, Element host, View view, XYZ head)
        {
            XYZ leaderEnd = LeaderLogicService.GetL1(tag);
            if (leaderEnd != null)
                return leaderEnd;

            if (host != null)
            {
                XYZ midpoint = SmartMepTagService.GetElementMidpoint(host, view);
                if (midpoint != null)
                    return midpoint;
            }

            return head;
        }
    }
}
