#region Metadata
/*
 * Tool Name     : Tag Stack Service
 * File Name     : TagStackService.cs
 * Purpose       : The ONE nearest-first vertical stacking loop. Rearrange Tags and Stack Tags each
 *                 had their own copy of it - the same loop, differing only in what they carried
 *                 (existing tags versus elements waiting for a tag) and what they did at each slot
 *                 (move a tag versus create or move one). Those two differences are now callbacks,
 *                 so the STACKING RULE itself exists once.
 *
 *                 The rule: the item nearest the clicked point takes the first slot. Whether the
 *                 stack then grows upward or downward is decided by where that first item sits
 *                 relative to the click - click above it and the stack runs up, click below and it
 *                 runs down. Every head is aligned to the clicked point's vertical line.
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
 * Dependencies  : Autodesk Revit API, AJTools.Services.LeaderLogic (LeaderLogicService),
 *                 AJTools.Utils (Constants)
 *
 * Input         : The items to stack, the clicked base point, the vertical gap, and two callbacks -
 *                 one saying where an item currently is, one placing it at a slot.
 * Output        : True when every item was placed. All-or-nothing: the first refusal stops the run
 *                 and the caller rolls its transaction back, which is what both tools already did.
 *
 * Notes         :
 * - Callers pre-filter their own list and check their own minimum count before calling. Rearrange
 *   Tags needs at least two tags and drops any whose leader end could not be read; Stack Tags is
 *   happy with one. Those are the tools' rules, not the stacking rule's.
 * - The placement callback receives a target that is ALREADY aligned to the base column, so neither
 *   caller has to remember to do it - forgetting that alignment is exactly the kind of drift that
 *   made the two copies diverge in the first place.
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release. Behaviour of both callers preserved exactly.
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
using AJTools.Services.TagClash;
using AJTools.Utils;

namespace AJTools.Services.TagArrange
{
    /// <summary>
    /// Nearest-first vertical stacking, shared by Rearrange Tags and Stack Tags.
    /// </summary>
    internal static class TagStackService
    {
        /// <summary>
        /// Fallback tag height, mm on the sheet, used only when nothing could be measured - which in
        /// practice means Stack Tags run in a view with no tags in it yet. Rearrange Tags always has
        /// the user's own selected tags to measure, so it never reaches this.
        /// </summary>
        private const double AssumedTagHeightPaperMm = 10.0;

        /// <summary>
        /// Turns the user's requested GAP between tags into the centre-to-centre step the stacking
        /// loop needs, by adding the height of the tallest tag.
        /// </summary>
        /// <param name="view">The active view - supplies scale and the view axes.</param>
        /// <param name="requestedGapMm">
        /// The clear space the user wants BETWEEN tags, mm on the sheet. 1mm means 1mm of visible gap.
        /// </param>
        /// <param name="measurableTags">
        /// Tags to measure. Rearrange Tags passes the tags the user selected, so the answer is exact.
        /// Stack Tags has not created its tags yet, so it passes the tags already in the view.
        /// </param>
        /// <returns>The centre-to-centre step in feet, ready for StackFromPoint.</returns>
        /// <remarks>
        /// The setting used to BE the step (v1.49.8 and earlier), and that was the bug Ajmal hit: he
        /// set 1-2mm expecting a tight gap and got tags on top of each other, because his tags are
        /// 12mm tall and the step was 1mm. A number typed as "spacing" means the space you can SEE
        /// between two tags - not the distance between their centres, which nobody can judge by eye
        /// without knowing the tag height first.
        ///
        /// Reading it as a gap also removes the whole class of problem: any positive gap is
        /// un-overlappable by construction, so the v1.49.8 "your spacing was too small, I raised it"
        /// guard and its message are gone. There is nothing left to guard against.
        ///
        /// Measures the TALLEST tag, not the average - one tall tag would otherwise overlap its
        /// neighbour while every other gap looked right.
        /// </remarks>
        internal static double ResolveVerticalStepFeet(
            View view,
            double requestedGapMm,
            IEnumerable<Element> measurableTags)
        {
            if (view == null)
                return (AssumedTagHeightPaperMm + Math.Max(0.0, requestedGapMm)) * Constants.MM_TO_FEET;

            int viewScale = TagViewGeometry.GetViewScale(view);
            double tallestPaperMm = 0.0;

            if (measurableTags != null)
            {
                XYZ viewRight = TagViewGeometry.GetViewRight(view);
                XYZ viewUp = TagViewGeometry.GetViewUp(view);

                foreach (Element tag in measurableTags)
                {
                    if (tag == null)
                        continue;

                    try
                    {
                        AnnotationBox box = TagViewGeometry.GetTagTextBox(tag, view, viewRight, viewUp);
                        if (box == null)
                            continue;

                        double heightPaperMm = TagViewGeometry.FeetToPaperMm(box.MaxY - box.MinY, viewScale);
                        if (heightPaperMm > tallestPaperMm)
                            tallestPaperMm = heightPaperMm;
                    }
                    catch (Exception)
                    {
                        // A tag that will not measure simply does not raise the height.
                    }
                }
            }

            if (tallestPaperMm <= 0.0)
                tallestPaperMm = AssumedTagHeightPaperMm;

            // Every return here is "mm on the sheet -> feet in the model" and MUST carry the view
            // scale. Dropping it on any one path makes the step scale-times too small - at 1:100 that
            // stacks every tag on one spot - and it still reads as a sensible unit conversion.
            return (tallestPaperMm + Math.Max(0.0, requestedGapMm)) * Constants.MM_TO_FEET * viewScale;
        }

        /// <summary>
        /// Places every item into a vertical stack starting at the clicked point.
        /// </summary>
        /// <param name="items">Items to stack. The caller has already filtered and count-checked these.</param>
        /// <param name="getAnchor">
        /// Where an item currently sits: its existing leader end for Rearrange Tags, the element's
        /// midpoint for Stack Tags. Used both to pick the nearest item and to decide the direction.
        /// </param>
        /// <param name="placeAt">
        /// Puts one item at one slot. The target is already aligned to the base column. Return false
        /// to abandon the whole arrangement.
        /// </param>
        internal static bool StackFromPoint<T>(
            IList<T> items,
            View activeView,
            LeaderLogicService leaderLogic,
            XYZ basePointModel,
            double verticalOffset,
            Func<T, XYZ> getAnchor,
            Func<T, XYZ, bool> placeAt)
            where T : class
        {
            if (items == null || items.Count == 0
                || activeView == null || leaderLogic == null || basePointModel == null
                || getAnchor == null || placeAt == null)
            {
                return false;
            }

            var remaining = new List<T>(items);

            double baseXView = leaderLogic.ProjectToView(basePointModel).U;

            T first = FindNearest(remaining, leaderLogic, basePointModel, getAnchor);
            if (first == null)
                return false;

            XYZ firstAnchor = getAnchor(first);
            if (firstAnchor == null)
                return false;

            // Clicked above the first item -> stack upward; clicked below -> stack downward.
            bool stackUp = IsAboveInView(basePointModel, firstAnchor, leaderLogic);

            if (!placeAt(first, AlignToBaseColumn(basePointModel, baseXView, leaderLogic)))
                return false;

            remaining.Remove(first);

            XYZ viewUp = ResolveViewUp(activeView);
            XYZ stepDirection = stackUp ? viewUp : -viewUp;
            XYZ lastPosition = basePointModel;

            while (remaining.Count > 0)
            {
                XYZ nextSlot = lastPosition.Add(stepDirection.Multiply(verticalOffset));

                T next = FindNearest(remaining, leaderLogic, nextSlot, getAnchor);
                if (next == null)
                    return false;

                if (!placeAt(next, AlignToBaseColumn(nextSlot, baseXView, leaderLogic)))
                    return false;

                remaining.Remove(next);
                lastPosition = nextSlot;
            }

            return true;
        }

        /// <summary>
        /// The item whose anchor is nearest the target, measured flat on the drawing. Falls back to
        /// the first item when no anchor can be read, exactly as both original copies did.
        /// </summary>
        private static T FindNearest<T>(
            IList<T> candidates,
            LeaderLogicService leaderLogic,
            XYZ target,
            Func<T, XYZ> getAnchor)
            where T : class
        {
            if (candidates == null || candidates.Count == 0 || target == null)
                return null;

            T nearest = null;
            double minDistance = double.MaxValue;

            foreach (T candidate in candidates)
            {
                XYZ anchor = candidate != null ? getAnchor(candidate) : null;
                if (anchor == null)
                    continue;

                double distance = DistanceInView(target, anchor, leaderLogic);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest ?? candidates[0];
        }

        private static double DistanceInView(XYZ a, XYZ b, LeaderLogicService leaderLogic)
        {
            UV ua = leaderLogic.ProjectToView(a);
            UV ub = leaderLogic.ProjectToView(b);
            double du = ua.U - ub.U;
            double dv = ua.V - ub.V;
            return Math.Sqrt((du * du) + (dv * dv));
        }

        private static bool IsAboveInView(XYZ target, XYZ reference, LeaderLogicService leaderLogic)
        {
            return leaderLogic.ProjectToView(target).V > leaderLogic.ProjectToView(reference).V;
        }

        /// <summary>Slides a point sideways onto the stack's vertical line.</summary>
        private static XYZ AlignToBaseColumn(XYZ pointModel, double baseXView, LeaderLogicService leaderLogic)
        {
            double deltaX = baseXView - leaderLogic.ProjectToView(pointModel).U;
            return leaderLogic.OffsetInView(pointModel, deltaX, 0);
        }

        private static XYZ ResolveViewUp(View activeView)
        {
            XYZ viewUp = activeView.UpDirection;
            if (viewUp == null || viewUp.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                return XYZ.BasisY;

            return viewUp.Normalize();
        }
    }
}
