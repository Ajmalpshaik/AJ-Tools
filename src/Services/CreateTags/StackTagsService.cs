// Tool Name: Stack Tags Service
// Description: Select MEP elements, then click ONE location - a fresh tag is created for every
//              eligible element and the whole batch is arranged into a vertical stack starting at
//              that point (nearest element first), exactly like Rearrange Tags' own click-to-place
//              behaviour. Clicking again relocates the WHOLE stack (moves the tags already created in
//              this run rather than creating duplicates) - press Esc once you're happy with where it
//              landed. Same eligibility rules as Create Tags (already tagged / too short / vertical),
//              same category + minimum-length settings, and the same stack spacing Rearrange Tags
//              itself uses - nothing new to configure for this tool specifically.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Models.SmartTag, AJTools.Models.CreateTags,
//               AJTools.Services.SmartTag, AJTools.Services.LeaderLogic, AJTools.Utils

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.CreateTags;
using AJTools.Models.SmartTag;
using AJTools.Services.LeaderLogic;
using AJTools.Services.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.CreateTags
{
    /// <summary>
    /// Orchestrates the Stack Tags pipeline: validate view -> filter the current selection down to
    /// eligible elements (shared with Create Tags) -> resolve a tag family for each -> let the user
    /// click a base point (nearest-element-first stacking, same technique as Rearrange Tags) ->
    /// create a tag the first time an element is placed, or move its already-created tag on later
    /// clicks that relocate the whole stack.
    /// </summary>
    internal static class StackTagsService
    {
        private const string ToolTitle = "Stack Tags";

        internal static Result Execute(ExternalCommandData commandData, ref string message)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Cancelled;
            }

            View activeView = doc.ActiveView;

            PreFlightResult preflight = SmartMepTagService.RunPreFlightChecks(doc, activeView);
            if (!preflight.Passed)
            {
                message = preflight.ErrorMessage;
                DialogHelper.ShowError(ToolTitle, preflight.ErrorMessage);
                return Result.Cancelled;
            }

            if (preflight.Warnings.Count > 0)
            {
                string warningText = string.Join("\n- ", preflight.Warnings);
                bool proceed = DialogHelper.ShowYesNo(
                    ToolTitle + " - Warnings",
                    string.Format("Pre-flight checks passed with warnings:\n\n- {0}\n\nDo you want to continue?", warningText));
                if (!proceed)
                    return Result.Cancelled;
            }

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                DialogHelper.ShowError(ToolTitle, "Select one or more elements to tag first, then run Stack Tags.");
                return Result.Cancelled;
            }

            var tracker = new CreateTagsSettingsTracker(doc);
            CreateTagsSettingsState settings = CreateTagsSettingsTracker.EnsureDefaults(tracker.LastState);
            double minLengthInternal = CreateTagsSettingsTracker.ResolveMinLengthInternal(settings);

            var tally = new SkipTally();
            HashSet<ElementId> alreadyTagged = SmartMepTagService.CollectAlreadyTaggedElementIds(doc, activeView);

            List<TagCandidate> candidates = CreateTagsEligibilityFilter.BuildEligibleCandidates(
                doc, activeView, selectedIds, settings, minLengthInternal, alreadyTagged, tally);

            if (candidates.Count == 0)
            {
                ShowSummary(0, tally, null);
                return Result.Cancelled;
            }

            var familyResults = new List<TagPlacementResult>();
            List<string> tagWarnings = SmartMepTagService.SelectTagFamilies(doc, preflight, candidates, familyResults);
            foreach (TagPlacementResult result in familyResults)
                tally.Add(string.Format("No tag family loaded for {0}", SmartTagSettingsTracker.GetCategoryLabel(result.Category)));

            if (candidates.Count == 0)
            {
                ShowSummary(0, tally, null);
                return Result.Cancelled;
            }

            int viewScale = Math.Max(activeView.Scale, 1);
            double spacingMm = TagArrangeSettings.GetTagSpacingMm();
            double verticalOffset = spacingMm * Constants.MM_TO_FEET * viewScale;

            LeaderLogicService leaderLogic = new LeaderLogicService(activeView);

            // Candidate ElementId -> its tag's ElementId, but only for tags a COMMITTED click actually
            // created - see the merge-on-commit comment below for why this can't just be updated live.
            var createdTagIds = new Dictionary<ElementId, ElementId>();
            bool hadCommit = false;

            using (TransactionGroup tg = new TransactionGroup(doc, "Stack Tags"))
            {
                tg.Start();

                while (true)
                {
                    XYZ basePointModel;
                    try
                    {
                        basePointModel = uidoc.Selection.PickPoint("Click a location for the tag stack (Esc to finish)");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    using (Transaction t = new Transaction(doc, "Stack Tags"))
                    {
                        t.Start();

                        // Tags created DURING this attempt only get merged into createdTagIds after
                        // the transaction actually commits. If this click's arrangement fails partway
                        // and rolls back, any tag it created is gone too - merging early would leave
                        // createdTagIds pointing at deleted elements for the next click.
                        var newlyCreatedThisAttempt = new Dictionary<ElementId, ElementId>();
                        bool ok;
                        try
                        {
                            ok = TryArrangeAtPoint(
                                doc, activeView, leaderLogic, candidates, createdTagIds,
                                newlyCreatedThisAttempt, basePointModel, verticalOffset);
                        }
                        catch (Exception)
                        {
                            ok = false;
                        }

                        if (ok)
                        {
                            t.Commit();
                            hadCommit = true;
                            foreach (KeyValuePair<ElementId, ElementId> kvp in newlyCreatedThisAttempt)
                                createdTagIds[kvp.Key] = kvp.Value;
                        }
                        else
                        {
                            t.RollBack();
                        }
                    }
                }

                if (hadCommit)
                    tg.Assimilate();
                else
                    tg.RollBack();
            }

            ShowSummary(createdTagIds.Count, tally, tagWarnings);

            return createdTagIds.Count > 0 ? Result.Succeeded : Result.Cancelled;
        }

        /// <summary>
        /// One click's worth of work: assigns every eligible candidate to a slot in a vertical stack
        /// starting at basePointModel (nearest element first, same as Rearrange Tags' T1-to-L1
        /// matching), creating or moving each one's tag in turn. All-or-nothing - if any candidate
        /// fails, the whole attempt fails and the caller rolls the transaction back.
        /// </summary>
        private static bool TryArrangeAtPoint(
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            List<TagCandidate> candidates,
            Dictionary<ElementId, ElementId> createdTagIds,
            Dictionary<ElementId, ElementId> newlyCreatedThisAttempt,
            XYZ basePointModel,
            double verticalOffset)
        {
            List<TagCandidate> remaining = new List<TagCandidate>(candidates);
            if (remaining.Count == 0)
                return false;

            TagCandidate first = CreateTagsEligibilityFilter.FindNearestCandidate(remaining, leaderLogic, basePointModel);
            if (first == null)
                return false;

            bool stackUp = IsAboveInView(basePointModel, first.Midpoint, leaderLogic);

            UV baseUv = leaderLogic.ProjectToView(basePointModel);
            double baseXView = baseUv.U;

            if (!PlaceOrMove(doc, activeView, leaderLogic, first, createdTagIds, newlyCreatedThisAttempt, basePointModel, baseXView))
                return false;

            remaining.Remove(first);

            XYZ viewUp = activeView.UpDirection;
            if (viewUp == null || viewUp.GetLength() <= Constants.ZERO_LENGTH_TOLERANCE)
                viewUp = XYZ.BasisY;
            else
                viewUp = viewUp.Normalize();

            XYZ stepDirection = stackUp ? viewUp : -viewUp;
            XYZ lastPosition = basePointModel;

            while (remaining.Count > 0)
            {
                XYZ nextSlot = lastPosition.Add(stepDirection.Multiply(verticalOffset));
                TagCandidate next = CreateTagsEligibilityFilter.FindNearestCandidate(remaining, leaderLogic, nextSlot);
                if (next == null)
                    return false;

                if (!PlaceOrMove(doc, activeView, leaderLogic, next, createdTagIds, newlyCreatedThisAttempt, nextSlot, baseXView))
                    return false;

                remaining.Remove(next);
                lastPosition = nextSlot;
            }

            return true;
        }

        private static bool IsAboveInView(XYZ targetModel, XYZ referenceModel, LeaderLogicService leaderLogic)
        {
            UV target = leaderLogic.ProjectToView(targetModel);
            UV reference = leaderLogic.ProjectToView(referenceModel);
            return target.V > reference.V;
        }

        private static XYZ AlignToBaseX(XYZ pointModel, double baseXView, LeaderLogicService leaderLogic)
        {
            UV uv = leaderLogic.ProjectToView(pointModel);
            double deltaX = baseXView - uv.U;
            return leaderLogic.OffsetInView(pointModel, deltaX, 0);
        }

        /// <summary>
        /// Creates the candidate's tag the first time it's placed in this run, or moves its
        /// already-created tag into the new slot on a later click.
        /// </summary>
        private static bool PlaceOrMove(
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            TagCandidate candidate,
            Dictionary<ElementId, ElementId> createdTagIds,
            Dictionary<ElementId, ElementId> newlyCreatedThisAttempt,
            XYZ targetModel,
            double baseXView)
        {
            XYZ finalTarget = AlignToBaseX(targetModel, baseXView, leaderLogic);
            if (finalTarget == null)
                return false;

            ElementId existingTagId;
            if (createdTagIds.TryGetValue(candidate.ElementId, out existingTagId))
            {
                IndependentTag existingTag = doc.GetElement(existingTagId) as IndependentTag;
                if (existingTag != null && existingTag.IsValidObject)
                    return TryMoveExistingTag(doc, existingTag, leaderLogic, finalTarget);
            }

            return TryCreateFreshTag(doc, activeView, leaderLogic, candidate, newlyCreatedThisAttempt, finalTarget);
        }

        private static bool TryCreateFreshTag(
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            TagCandidate candidate,
            Dictionary<ElementId, ElementId> newlyCreatedThisAttempt,
            XYZ tagPosition)
        {
            if (candidate.TagTypeId == null || candidate.TagTypeId == ElementId.InvalidElementId)
                return false;

            Element targetElement = doc.GetElement(candidate.ElementId);
            if (targetElement == null)
                return false;

            Reference elemRef = new Reference(targetElement);

            IndependentTag newTag = IndependentTag.Create(
                doc,
                activeView.Id,
                elemRef,
                true,
                TagMode.TM_ADDBY_CATEGORY,
                TagOrientation.Horizontal,
                tagPosition);

            if (newTag == null)
                return false;

            try
            {
                newTag.ChangeTypeId(candidate.TagTypeId);
            }
            catch (Exception)
            {
                // If type change fails, the default type was used - still acceptable.
            }

            if (!ApplyFreshLeader(doc, newTag, leaderLogic))
                return false;

            newlyCreatedThisAttempt[candidate.ElementId] = newTag.Id;
            return true;
        }

        /// <summary>
        /// Attaches a plain L-shaped leader to a freshly created tag - same ComputeElbow + plain
        /// TrySetLeaderElbow technique as Rearrange Tags' own TryApplyLShapeLeader (no outside-text-
        /// bounds nudge, no toggle-the-leader-condition fallback - Rearrange Tags deliberately avoids
        /// both, per its own comment: "do not toggle leader end condition as fallback"). Only the L1
        /// rollback-probe is kept, since that's a Revit API read quirk, not a style choice - a freshly
        /// created tag's L1 isn't always readable via GetL1 directly.
        /// </summary>
        private static bool ApplyFreshLeader(Document doc, IndependentTag tag, LeaderLogicService leaderLogic)
        {
            try
            {
                if (!tag.HasLeader)
                    tag.HasLeader = true;
            }
            catch (Exception)
            {
            }

            bool hasLeader;
            try
            {
                hasLeader = tag.HasLeader;
            }
            catch (Exception)
            {
                hasLeader = false;
            }

            if (!hasLeader)
                return true;

            XYZ head;
            try
            {
                head = tag.TagHeadPosition;
            }
            catch (Exception)
            {
                return true;
            }

            if (head == null)
                return true;

            XYZ l1 = LeaderLogicService.GetL1(tag);
            if (l1 == null)
                l1 = TryResolveLeaderEndByRollbackProbe(doc, tag);

            if (l1 == null)
                return true;

            XYZ elbow = leaderLogic.ComputeElbow(head, l1);
            if (elbow == null)
                return true;

            return LeaderLogicService.TrySetLeaderElbow(tag, elbow);
        }

        private static XYZ TryResolveLeaderEndByRollbackProbe(Document doc, IndependentTag tag)
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
        /// Moves an already-created tag to a new head position and re-computes its L-shaped elbow -
        /// same technique as Rearrange Tags' own TryMoveTag, reading L1 fresh each time since nothing
        /// else touches this tag's leader end between our own clicks.
        /// </summary>
        private static bool TryMoveExistingTag(
            Document doc,
            IndependentTag tag,
            LeaderLogicService leaderLogic,
            XYZ finalTarget)
        {
            if (tag.Pinned)
                return false;

            XYZ currentHead;
            try
            {
                currentHead = tag.TagHeadPosition;
            }
            catch (Exception)
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
                catch (Exception)
                {
                    return false;
                }
            }

            bool hasLeader;
            try
            {
                hasLeader = tag.HasLeader;
            }
            catch (Exception)
            {
                hasLeader = false;
            }

            if (!hasLeader)
                return true;

            XYZ finalHead = finalTarget;
            try
            {
                XYZ refreshed = tag.TagHeadPosition;
                if (refreshed != null)
                    finalHead = refreshed;
            }
            catch (Exception)
            {
            }

            XYZ l1 = LeaderLogicService.GetL1(tag);
            if (l1 == null)
                return true;

            XYZ elbow = leaderLogic.ComputeElbow(finalHead, l1);
            if (elbow == null)
                return true;

            return LeaderLogicService.TrySetLeaderElbow(tag, elbow);
        }

        private static void ShowSummary(int stackedCount, SkipTally tally, List<string> tagWarnings)
        {
            string summary = tally.BuildSummary();
            string body = string.Format("{0} tag(s) in the stack.", stackedCount);
            if (summary != null)
                body += "\n\nSkipped:\n" + summary;

            if (tagWarnings != null && tagWarnings.Count > 0)
                body += "\n\nNote:\n- " + string.Join("\n- ", tagWarnings);

            DialogHelper.ShowInfo(ToolTitle, body);
        }
    }
}
