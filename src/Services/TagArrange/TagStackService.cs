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
        /// Works out a vertical step that cannot make the stacked tags overlap, and reports whether
        /// the user's own spacing had to be raised to get there.
        /// </summary>
        /// <param name="view">The active view - supplies scale and the view axes.</param>
        /// <param name="requestedSpacingMm">The spacing from Arrange Tags Settings, mm on the sheet.</param>
        /// <param name="measurableTags">
        /// Tags that can actually be measured. Rearrange Tags passes the tags the user selected, so the
        /// answer is exact. Stack Tags has not created its tags yet at this point, so it passes the tags
        /// already in the view as a stand-in.
        /// </param>
        /// <param name="appliedSpacingMm">The spacing actually used, mm on the sheet.</param>
        /// <param name="wasRaised">True when the setting was too small and had to be raised.</param>
        /// <returns>The step in feet, ready to hand to StackFromPoint.</returns>
        /// <remarks>
        /// Why this exists (v1.49.8, Ajmal's question): the spacing setting is a plain number that knows
        /// nothing about the tags. It steps from one tag's POSITION to the next, never measuring how tall
        /// a tag is - so a two-line tag, or a taller tag family, silently overlaps at a spacing that
        /// looked fine before. Neither stacking tool does any clash checking, so nothing catches it.
        ///
        /// This does NOT replace the setting with clash detection, which was the other option
        /// considered. Clash detection only guarantees "not touching", and it moves each tag by the
        /// shortest distance that clears - which on a column of mixed-length tags produces UNEVEN gaps.
        /// An even column is the whole point of Rearrange Tags. So the setting stays and keeps meaning
        /// "how far apart I want them"; this only stops it being set too small to work.
        ///
        /// Measuring finds the TALLEST tag, not the average: one tall tag in the batch would otherwise
        /// overlap its neighbour while every other gap looked right.
        /// </remarks>
        internal static double ResolveSafeVerticalOffsetFeet(
            View view,
            double requestedSpacingMm,
            IEnumerable<Element> measurableTags,
            out double appliedSpacingMm,
            out bool wasRaised)
        {
            appliedSpacingMm = requestedSpacingMm;
            wasRaised = false;

            // Every return below is "mm on the sheet -> feet in the model", which MUST include the view
            // scale. Dropping it on any one path makes the step scale-times too small - at 1:100 that
            // stacks every tag on the same spot - and it is silent, because the code still reads as a
            // sensible unit conversion.
            if (view == null)
                return requestedSpacingMm * Constants.MM_TO_FEET;

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
                        // A tag that will not measure simply does not raise the floor.
                    }
                }
            }

            // Nothing measurable - there is no honest basis to override the user, so leave it alone.
            if (tallestPaperMm <= 0.0)
                return requestedSpacingMm * Constants.MM_TO_FEET * viewScale;

            // The same minimum gap the clash engine uses, so a stack this tool calls tidy is also a
            // stack Fix Tag Clash agrees is clear.
            double minGapMm = TagClashSettings.Load().MinGapMm;
            double safestSpacingMm = tallestPaperMm + minGapMm;

            if (requestedSpacingMm < safestSpacingMm)
            {
                appliedSpacingMm = safestSpacingMm;
                wasRaised = true;
            }

            return appliedSpacingMm * Constants.MM_TO_FEET * viewScale;
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
