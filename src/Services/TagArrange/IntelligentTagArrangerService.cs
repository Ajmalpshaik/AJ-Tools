// Tool Name: Intelligent Tag Arranger Service
// Description: Converts the pyRevit intelligent tag arranger workflow into native C#.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.LeaderLogic, AJTools.Utils

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Services.LeaderLogic;
using AJTools.Utils;

namespace AJTools.Services.TagArrange
{
    /// <summary>
    /// Rearranges selected tags using nearest-first assignment based on T1/L1 distance.
    /// </summary>
    internal static class IntelligentTagArrangerService
    {
        private const string ToolTitle = "Intelligent Tag Arranger";

        private sealed class TagData
        {
            public TagData(IndependentTag tag, XYZ originalLeaderStart)
            {
                Tag = tag;
                OriginalLeaderStart = originalLeaderStart;
            }

            public IndependentTag Tag { get; private set; }
            public XYZ OriginalLeaderStart { get; private set; }
        }

        internal static Result Execute(ExternalCommandData commandData, ref string message)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            if (doc.IsReadOnly)
            {
                DialogHelper.ShowError(ToolTitle, "The document is read-only.");
                return Result.Cancelled;
            }

            View activeView = doc.ActiveView;
            if (activeView == null || activeView.IsTemplate)
            {
                DialogHelper.ShowError(ToolTitle, "Please run this tool in a normal project view.");
                return Result.Cancelled;
            }

            List<IndependentTag> selectedTags = CollectSelectedTags(doc, uidoc.Selection.GetElementIds());
            if (selectedTags.Count < 2)
            {
                DialogHelper.ShowError(ToolTitle, "Please select at least two tags to arrange.");
                return Result.Cancelled;
            }

            int selectedCount = selectedTags.Count;
            List<TagData> allTags = BuildTagData(doc, selectedTags);
            if (allTags.Count < 2)
            {
                DialogHelper.ShowError(
                    ToolTitle,
                    $"You selected {selectedCount} tag(s), but only {allTags.Count} tag(s) had a readable existing leader start (L1).\n" +
                    "This tool only uses each tag's current L1 and will not replace it.");
                return Result.Cancelled;
            }

            // Ask before a long run. Every click re-arranges the WHOLE selection into a fresh stack
            // (see the whole-batch-per-click note in ajtools-conventions.md), so one click on a large
            // selection means a silent freeze with nothing to press.
            if (!DialogHelper.ConfirmLongRun(
                    ToolTitle,
                    allTags.Count,
                    string.Format(
                        "About to arrange {0} tags into a stack on every click.",
                        allTags.Count)))
            {
                return Result.Cancelled;
            }

            LeaderLogicService leaderLogic = new LeaderLogicService(activeView);
            int viewScale = Math.Max(activeView.Scale, 1);
            double spacingMm = TagArrangeSettings.GetTagSpacingMm();
            double verticalOffset = spacingMm * Constants.MM_TO_FEET * viewScale;

            bool hadCommit = false;
            int undoneAttempts = 0;
            using (TransactionGroup tg = new TransactionGroup(doc, "Arrange Tags"))
            {
                tg.Start();

                while (true)
                {
                    XYZ basePointModel;
                    try
                    {
                        basePointModel = uidoc.Selection.PickPoint("Click a base location (Press ESC when satisfied)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    using (Transaction t = new Transaction(doc, "Try Arrangement"))
                    {
                        t.Start();
                        try
                        {
                            bool ok = TryArrangeAtPoint(doc, activeView, leaderLogic, allTags, basePointModel, verticalOffset);
                            if (ok)
                            {
                                t.Commit();
                                hadCommit = true;
                            }
                            else
                            {
                                // All-or-nothing by design: one tag that cannot be placed undoes the
                                // whole click. Counted so the run can say so at the end - this used to
                                // roll back in silence, which looks exactly like the click doing nothing.
                                t.RollBack();
                                undoneAttempts++;
                            }
                        }
                        catch (Exception)
                        {
                            if (t.GetStatus() != TransactionStatus.RolledBack && t.GetStatus() != TransactionStatus.Committed)
                            {
                                t.RollBack();
                            }
                            break;
                        }
                    }
                }

                if (hadCommit)
                    tg.Assimilate();
                else
                    tg.RollBack();
            }

            if (!hadCommit && undoneAttempts > 0)
            {
                DialogHelper.ShowError(
                    ToolTitle,
                    string.Format(
                        "Nothing was moved.\n\n"
                        + "You clicked {0} time(s), and each attempt was undone because at least one tag "
                        + "in the selection could not be placed. Arranging is all-or-nothing, so a single "
                        + "problem tag stops the whole stack.\n\n"
                        + "The usual causes are a pinned tag, or a tag whose leader end cannot be read.",
                        undoneAttempts));
            }

            return hadCommit ? Result.Succeeded : Result.Cancelled;
        }

        private static List<IndependentTag> CollectSelectedTags(Document doc, ICollection<ElementId> selectedIds)
        {
            List<IndependentTag> tags = new List<IndependentTag>();
            if (selectedIds == null || selectedIds.Count == 0)
                return tags;

            foreach (ElementId id in selectedIds)
            {
                IndependentTag tag = doc.GetElement(id) as IndependentTag;
                if (tag != null)
                    tags.Add(tag);
            }

            return tags;
        }

        private static List<TagData> BuildTagData(Document doc, IList<IndependentTag> selectedTags)
        {
            List<TagData> data = new List<TagData>();
            if (doc == null || selectedTags == null)
                return data;

            List<IndependentTag> unresolved = new List<IndependentTag>();

            foreach (IndependentTag tag in selectedTags)
            {
                if (tag == null)
                    continue;

                XYZ existingL1 = LeaderLogicService.GetL1(tag);
                if (existingL1 == null)
                {
                    unresolved.Add(tag);
                    continue;
                }

                data.Add(new TagData(tag, existingL1));
            }

            if (unresolved.Count > 0)
                TryResolveLeaderStartsByRollbackProbe(doc, unresolved, data);

            return data;
        }

        private static void TryResolveLeaderStartsByRollbackProbe(
            Document doc,
            IList<IndependentTag> unresolved,
            IList<TagData> output)
        {
            if (doc == null || unresolved == null || unresolved.Count == 0 || output == null)
                return;

            using (Transaction t = new Transaction(doc, "Probe Tag Leader Starts"))
            {
                t.Start();

                foreach (IndependentTag tag in unresolved)
                {
                    if (tag == null || !tag.IsValidObject)
                        continue;

                    // The probe itself is shared - this file used to carry its own copy of the
                    // sub-transaction dance. The outer Transaction stays here because BuildTagData
                    // runs before any transaction is open, and a SubTransaction needs one.
                    XYZ probed = TagLeaderService.TryResolveLeaderEndByRollbackProbe(doc, tag);
                    if (probed != null)
                        output.Add(new TagData(tag, probed));
                }

                t.RollBack();
            }
        }

        private static bool TryArrangeAtPoint(
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            IList<TagData> allTags,
            XYZ basePointModel,
            double verticalOffset)
        {
            if (doc == null || activeView == null || leaderLogic == null || basePointModel == null || allTags == null)
                return false;

            // This tool's own rules: only tags that still exist and whose leader end was readable, and
            // at least two of them - there is nothing to arrange with one tag.
            List<TagData> arrangeable = new List<TagData>();
            foreach (TagData data in allTags)
            {
                if (data != null && data.Tag != null && data.Tag.IsValidObject && data.OriginalLeaderStart != null)
                    arrangeable.Add(data);
            }

            if (arrangeable.Count < 2)
                return false;

            // The stacking rule itself is shared with Stack Tags - see TagStackService. This tool
            // anchors on each tag's EXISTING leader end, which is what keeps L1 where it was.
            return TagStackService.StackFromPoint(
                arrangeable,
                activeView,
                leaderLogic,
                basePointModel,
                verticalOffset,
                data => data.OriginalLeaderStart,
                (data, target) => TryMoveTag(data, doc, activeView, leaderLogic, target));
        }

        // Removed 2026-08-16: FindNearestTagForTarget, DistanceInView and IsT1AboveL1. Stack Tags
        // had its own copy of all three. They are part of the stacking RULE, not of this tool, and
        // now live once in TagStackService alongside the loop that uses them.

        /// <summary>
        /// Moves one tag to a slot. The target arrives already aligned to the stack's column -
        /// TagStackService does that now, so this method no longer takes a base X.
        /// </summary>
        private static bool TryMoveTag(
            TagData data,
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            XYZ finalTarget)
        {
            if (data == null || data.Tag == null || doc == null || leaderLogic == null || finalTarget == null)
                return false;

            IndependentTag tag = data.Tag;
            if (tag.Pinned)
                return false;

            XYZ currentHead;
            try
            {
                currentHead = tag.TagHeadPosition;
            }
            catch
            {
                return false;
            }

            if (currentHead == null)
                return false;

            XYZ move = finalTarget - currentHead;
            if (move.GetLength() > Constants.ZERO_LENGTH_TOLERANCE)
            {
                try
                {
                    ElementTransformUtils.MoveElement(doc, tag.Id, move);
                }
                catch
                {
                    return false;
                }
            }

            XYZ finalHead = finalTarget;
            try
            {
                XYZ refreshed = tag.TagHeadPosition;
                if (refreshed != null)
                    finalHead = refreshed;
            }
            catch
            {
            }

            XYZ leaderEnd = data.OriginalLeaderStart;
            if (leaderEnd == null)
                return false;

            // PreserveLeaderEnd(useRollbackProbe: false) plus the ORIGINAL leader end captured before
            // the move. This is the whole point of the tool: L1 stays exactly where it was, so no
            // outside-text nudge and no toggle-the-leader-condition fallback - both of those would
            // move where the leader meets the element.
            return TagLeaderService.ApplyLShapedLeader(
                doc, tag, activeView, leaderLogic,
                TagLeaderOptions.PreserveLeaderEnd(false),
                leaderEndOverride: leaderEnd,
                headFallback: finalHead);
        }

        // Removed 2026-08-16: AlignToBaseX - Stack Tags had a character-for-character copy of it. The
        // column alignment now happens once, inside TagStackService, before a target ever reaches
        // TryMoveTag, so neither tool can forget to apply it.

        // Removed 2026-08-16: TryEnsureLeaderEnabled, TryHasLeader and TryApplyLShapeLeader were
        // this file's own copy of work Smart MEP Tags, Stack Tags and L-Shape Leader each also kept
        // privately. TryMoveTag above now calls Services/LeaderLogic/TagLeaderService.cs with
        // PreserveLeaderEnd, which carries the exact rule this tool depends on - keep L1 as-is, no
        // outside-text nudge and no leader-end-condition fallback. Behaviour is unchanged.

    }
}
