#region Metadata
/*
 * Tool Name     : Tag Leader Service
 * File Name     : TagLeaderService.cs
 * Purpose       : The ONE place that attaches an L-shaped leader to a tag. Before this file the same
 *                 sequence - switch the leader on, find where it meets the element, work out the
 *                 elbow, set it - existed FOUR times over, and the copies had quietly drifted apart:
 *
 *                   SmartTagPlacementEngine.ApplyLeaderBehavior      nudge + retry + probe
 *                   CreateTagsService (via the above)                nudge + retry + probe
 *                   StackTagsService.ApplyFreshLeader                no nudge, no retry, probe
 *                   StackTagsService.TryMoveExistingTag              no nudge, no retry, no probe
 *                   IntelligentTagArrangerService.TryApplyLShapeLeader   no nudge, no retry, given L1
 *
 *                 Those differences are now OPTIONS on one routine rather than four bodies of code, so
 *                 each tool keeps exactly the behaviour it had while a fix to the leader itself lands
 *                 everywhere at once.
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
 * Dependencies  : Autodesk Revit API, AJTools.Services.TagClash (TagViewGeometry),
 *                 AJTools.Utils (Constants)
 *
 * Input         : A tag, the active view, a LeaderLogicService, and the options for this tool.
 * Output        : True when the leader was left in an acceptable state.
 *
 * Notes         :
 * - NOT every difference between the old copies was a bug. Rearrange Tags deliberately refuses to touch
 *   the leader end ("Keep L1 exactly as-is: do not toggle leader end condition as fallback") because it
 *   works on tags already placed, and forcing the end would move where the leader meets the duct. That
 *   is why RetryWithFreeCondition is an option and not simply switched on for everyone.
 * - PreserveLeaderEnd() and Tidy() are the two shapes actually in use; both are named after what they
 *   promise the caller, not after which tool happens to use them today.
 * - Returning true does NOT always mean an elbow was set. A tag with no leader, no readable leader end,
 *   or a straight-line leader that needs no elbow are all normal outcomes, not failures - this mirrors
 *   what all four originals did, and callers rely on it (a false result rolls their transaction back).
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release. Behaviour of each caller preserved exactly via options.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.DB;
using AJTools.Services.TagClash;
using AJTools.Utils;

namespace AJTools.Services.LeaderLogic
{
    /// <summary>
    /// How one tool wants its leaders handled. The defaults are the cautious ones: touch nothing that
    /// is not required.
    /// </summary>
    internal sealed class TagLeaderOptions
    {
        /// <summary>Switch the leader on when the tag has none. Every caller wants this.</summary>
        public bool EnableLeaderIfMissing { get; set; }

        /// <summary>
        /// When the leader end will not read directly, try setting the end condition to Free inside a
        /// rolled-back sub-transaction just to read the value. Needed for FRESHLY created tags, whose
        /// leader end is not always readable straight away; pointless for tags that already existed.
        /// </summary>
        public bool UseRollbackProbe { get; set; }

        /// <summary>Push the elbow clear of the tag text when it would otherwise sit under it.</summary>
        public bool NudgeElbowClearOfText { get; set; }

        /// <summary>
        /// If Revit refuses the elbow, set the leader end condition to Free, set the elbow, then put
        /// the condition back. This MOVES the leader end, which is exactly what Rearrange Tags must
        /// never do - leave it off for any tool working on tags that already exist.
        /// </summary>
        public bool RetryWithFreeCondition { get; set; }

        /// <summary>Tidy up everything: for tags this tool has just created and fully owns.</summary>
        public static TagLeaderOptions Tidy()
        {
            return new TagLeaderOptions
            {
                EnableLeaderIfMissing = true,
                UseRollbackProbe = true,
                NudgeElbowClearOfText = true,
                RetryWithFreeCondition = true
            };
        }

        /// <summary>
        /// Re-shape the elbow but leave the leader end exactly where it is: for tags that already
        /// exist, where moving the end would change where the leader meets the element.
        /// </summary>
        public static TagLeaderOptions PreserveLeaderEnd(bool useRollbackProbe)
        {
            return new TagLeaderOptions
            {
                EnableLeaderIfMissing = true,
                UseRollbackProbe = useRollbackProbe,
                NudgeElbowClearOfText = false,
                RetryWithFreeCondition = false
            };
        }
    }

    /// <summary>
    /// Attaches an L-shaped leader to a tag. One implementation, shared by every tag tool.
    /// </summary>
    internal static class TagLeaderService
    {
        private const double ElbowOutsideTextMarginMm = 3.0;

        /// <summary>
        /// Switches the leader on if needed, works out the L-shaped elbow, and sets it.
        /// </summary>
        /// <param name="leaderEndOverride">
        /// A leader end captured earlier. Rearrange Tags passes the value it read before moving the
        /// tag, so the leader end it re-uses is guaranteed to be the original one.
        /// </param>
        /// <param name="headFallback">
        /// Head position to fall back on when the tag will not report its own. Callers that have just
        /// moved a tag pass the point they moved it to.
        /// </param>
        internal static bool ApplyLShapedLeader(
            Document doc,
            IndependentTag tag,
            View view,
            LeaderLogicService leaderLogic,
            TagLeaderOptions options,
            XYZ leaderEndOverride = null,
            XYZ headFallback = null)
        {
            if (doc == null || tag == null || leaderLogic == null)
                return true;

            if (options == null)
                options = TagLeaderOptions.PreserveLeaderEnd(false);

            if (options.EnableLeaderIfMissing)
                TryEnableLeader(tag);

            if (!HasLeader(tag))
                return true;

            XYZ head = ReadHeadPosition(tag) ?? headFallback;
            if (head == null)
                return true;

            XYZ leaderEnd = leaderEndOverride ?? ResolveLeaderEnd(doc, tag, options.UseRollbackProbe);
            if (leaderEnd == null)
                return true;

            XYZ elbow = leaderLogic.ComputeElbow(head, leaderEnd);
            if (elbow == null)
                return true; // Straight-line leader - Revit draws it without an elbow.

            if (options.NudgeElbowClearOfText)
                elbow = NudgeElbowClearOfText(tag, view, leaderLogic, elbow);

            if (LeaderLogicService.TrySetLeaderElbow(tag, elbow))
                return true;

            if (!options.RetryWithFreeCondition)
                return false;

            return TrySetElbowPreservingCondition(tag, elbow);
        }

        /// <summary>
        /// Reads the leader end, optionally falling back to the rolled-back probe for freshly created
        /// tags whose leader end will not read directly.
        /// </summary>
        internal static XYZ ResolveLeaderEnd(Document doc, IndependentTag tag, bool useRollbackProbe)
        {
            XYZ leaderEnd = LeaderLogicService.GetL1(tag);
            if (leaderEnd != null || !useRollbackProbe)
                return leaderEnd;

            return TryResolveLeaderEndByRollbackProbe(doc, tag);
        }

        /// <summary>
        /// Reads the leader end by setting the end condition to Free inside a sub-transaction that is
        /// always rolled back, so the model is never actually changed. This exists because a freshly
        /// created tag does not always report its leader end until something touches it - a Revit read
        /// quirk, not a style choice, which is why even the tools that refuse every other leader change
        /// still use it.
        /// </summary>
        internal static XYZ TryResolveLeaderEndByRollbackProbe(Document doc, IndependentTag tag)
        {
            if (doc == null || tag == null || !tag.IsValidObject)
                return null;

            try
            {
                using (SubTransaction st = new SubTransaction(doc))
                {
                    st.Start();

                    XYZ probed = null;
                    try
                    {
                        if (LeaderLogicService.TrySetLeaderEndCondition(tag, LeaderEndCondition.Free))
                            probed = LeaderLogicService.GetL1(tag);
                    }
                    catch (Exception)
                    {
                        // A tag that refuses the probe simply has no readable leader end.
                    }

                    st.RollBack();
                    return probed;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Moves the elbow out past the right-hand edge of the tag text when it would otherwise sit
        /// inside it. Leaves it alone when it is already clear.
        /// </summary>
        internal static XYZ NudgeElbowClearOfText(
            Element tag, View view, LeaderLogicService leaderLogic, XYZ elbow)
        {
            if (tag == null || leaderLogic == null || elbow == null)
                return elbow;

            XYZ viewRight = TagViewGeometry.GetViewRight(view);
            XYZ viewUp = TagViewGeometry.GetViewUp(view);

            var textBox = TagViewGeometry.GetTagTextBox(tag, view, viewRight, viewUp);
            if (textBox == null)
                return elbow;

            UV elbowUv = leaderLogic.ProjectToView(elbow);
            bool inside = elbowUv.U >= textBox.MinX && elbowUv.U <= textBox.MaxX
                       && elbowUv.V >= textBox.MinY && elbowUv.V <= textBox.MaxY;
            if (!inside)
                return elbow;

            double marginFeet = TagViewGeometry.PaperMmToFeet(
                ElbowOutsideTextMarginMm, TagViewGeometry.GetViewScale(view));

            return leaderLogic.OffsetInView(elbow, (textBox.MaxX + marginFeet) - elbowUv.U, 0);
        }

        /// <summary>
        /// Moves the elbow clear of the tag text vertically - above when the element is above, below
        /// when it is below. Used for VERTICAL tag text, where the leader comes in top or bottom
        /// rather than from the side.
        /// </summary>
        internal static XYZ NudgeElbowClearOfTextVertical(
            Element tag, View view, LeaderLogicService leaderLogic, XYZ elbow, XYZ head, XYZ leaderEnd)
        {
            if (tag == null || leaderLogic == null || elbow == null || head == null || leaderEnd == null)
                return elbow;

            XYZ viewRight = TagViewGeometry.GetViewRight(view);
            XYZ viewUp = TagViewGeometry.GetViewUp(view);

            var textBox = TagViewGeometry.GetTagTextBox(tag, view, viewRight, viewUp);
            if (textBox == null)
                return elbow;

            UV elbowUv = leaderLogic.ProjectToView(elbow);
            bool inside = elbowUv.U >= textBox.MinX && elbowUv.U <= textBox.MaxX
                       && elbowUv.V >= textBox.MinY && elbowUv.V <= textBox.MaxY;
            if (!inside)
                return elbow;

            double marginFeet = TagViewGeometry.PaperMmToFeet(
                ElbowOutsideTextMarginMm, TagViewGeometry.GetViewScale(view));

            UV headUv = leaderLogic.ProjectToView(head);
            UV endUv = leaderLogic.ProjectToView(leaderEnd);

            double targetV = endUv.V < headUv.V
                ? textBox.MinY - marginFeet   // element below - push the elbow to the bottom
                : textBox.MaxY + marginFeet;  // element above - push the elbow to the top

            return leaderLogic.OffsetInView(elbow, 0, targetV - elbowUv.V);
        }

        /// <summary>
        /// Last resort when Revit refuses the elbow outright: free the leader end, set the elbow, then
        /// put the end condition back the way it was.
        /// </summary>
        private static bool TrySetElbowPreservingCondition(IndependentTag tag, XYZ elbow)
        {
            LeaderEndCondition initialCondition;
            bool hadInitialCondition =
                LeaderLogicService.TryGetLeaderEndCondition(tag, out initialCondition);

            if (!LeaderLogicService.TrySetLeaderEndCondition(tag, LeaderEndCondition.Free))
                return false;

            if (!LeaderLogicService.TrySetLeaderElbow(tag, elbow))
                return false;

            if (hadInitialCondition
                && !LeaderLogicService.TrySetLeaderEndCondition(tag, initialCondition))
            {
                return false;
            }

            return true;
        }

        private static void TryEnableLeader(IndependentTag tag)
        {
            try
            {
                if (!tag.HasLeader)
                    tag.HasLeader = true;
            }
            catch (Exception)
            {
                // Best effort - HasLeader is re-read below, so a refusal is handled there.
            }
        }

        private static bool HasLeader(IndependentTag tag)
        {
            try
            {
                return tag.HasLeader;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static XYZ ReadHeadPosition(IndependentTag tag)
        {
            try
            {
                return tag.TagHeadPosition;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
