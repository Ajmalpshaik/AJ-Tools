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
using AJTools.Services.TagArrange;
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

            // Ask before a long run. Unlike Create Tags - which asks for one click per tag and shows the
            // remaining count as it goes - a single click here creates and stacks a tag for EVERY
            // selected element, so a big selection means one click then a silent freeze.
            if (!DialogHelper.ConfirmLongRun(
                    ToolTitle,
                    candidates.Count,
                    string.Format(
                        "About to create and stack {0} tags from one click.",
                        candidates.Count)))
            {
                return Result.Cancelled;
            }

            double gapMm = TagArrangeSettings.GetTagSpacingMm();

            // The setting is the GAP the user wants to SEE between tags; the tallest tag's own height
            // is added to get the centre-to-centre step. This tool's own tags do not exist yet - they
            // are created as the user clicks - so the tags ALREADY in the view stand in for them.
            // Same view, same scale, in practice the same families. An empty view falls back to an
            // assumed tag height rather than guessing zero.
            double verticalOffset = TagStackService.ResolveVerticalStepFeet(
                activeView, gapMm, CollectExistingTagsInView(doc, activeView));

            LeaderLogicService leaderLogic = new LeaderLogicService(activeView);

            // Candidate ElementId -> its tag's ElementId, but only for tags a COMMITTED click actually
            // created - see the merge-on-commit comment below for why this can't just be updated live.
            var createdTagIds = new Dictionary<ElementId, ElementId>();
            bool hadCommit = false;
            int undoneAttempts = 0;

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
                            // All-or-nothing by design: one element that cannot be tagged undoes the
                            // whole click. Counted so the run can say so at the end - this used to roll
                            // back in silence, which looks exactly like the click doing nothing.
                            t.RollBack();
                            undoneAttempts++;
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
                        "No tags were created.\n\n"
                        + "You clicked {0} time(s), and each attempt was undone because at least one of "
                        + "the selected elements could not be tagged. Stacking is all-or-nothing, so a "
                        + "single problem element stops the whole stack.\n\n"
                        + "Try again with a smaller selection to find the element causing it.",
                        undoneAttempts));
                return Result.Cancelled;
            }

            // No "spacing was increased" note any more (it existed in v1.49.8/9). The setting is now
            // the GAP between tags, so any positive value works and nothing gets overridden.
            ShowSummary(createdTagIds.Count, tally, tagWarnings);

            return createdTagIds.Count > 0 ? Result.Succeeded : Result.Cancelled;
        }

        /// <summary>
        /// One click's worth of work: assigns every eligible candidate to a slot in a vertical stack
        /// starting at basePointModel (nearest element first, same as Rearrange Tags' T1-to-L1
        /// matching), creating or moving each one's tag in turn. All-or-nothing - if any candidate
        /// fails, the whole attempt fails and the caller rolls the transaction back.
        /// </summary>
        /// <summary>
        /// Every tag already in the view, used only to gauge how tall a tag is before this tool has
        /// created any of its own. Read-only - nothing here is modified.
        /// </summary>
        private static List<Element> CollectExistingTagsInView(Document doc, View view)
        {
            var tags = new List<Element>();

            if (doc == null || view == null)
                return tags;

            try
            {
                foreach (Element element in new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType())
                {
                    if (element != null)
                        tags.Add(element);
                }
            }
            catch (Exception)
            {
                // Measuring is a nicety - an unreadable view just means the setting is used as set.
            }

            return tags;
        }

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
            if (candidates == null || candidates.Count == 0)
                return false;

            // The stacking rule itself is shared with Rearrange Tags - see TagStackService. This tool
            // anchors on the ELEMENT's midpoint, because most of these tags do not exist yet.
            return TagStackService.StackFromPoint(
                candidates,
                activeView,
                leaderLogic,
                basePointModel,
                verticalOffset,
                candidate => candidate.Midpoint,
                (candidate, target) => PlaceOrMove(
                    doc, activeView, leaderLogic, candidate, createdTagIds, newlyCreatedThisAttempt, target));
        }

        // Removed 2026-08-16: IsAboveInView and AlignToBaseX. Rearrange Tags had its own copy of both,
        // and AlignToBaseX was character-for-character identical. They belong to the stacking rule
        // rather than to this tool, and now live once in TagStackService.

        /// <summary>
        /// Creates the candidate's tag the first time it's placed in this run, or moves its
        /// already-created tag into the new slot on a later click. The target arrives already aligned
        /// to the stack's column - TagStackService does that now.
        /// </summary>
        private static bool PlaceOrMove(
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            TagCandidate candidate,
            Dictionary<ElementId, ElementId> createdTagIds,
            Dictionary<ElementId, ElementId> newlyCreatedThisAttempt,
            XYZ finalTarget)
        {
            if (finalTarget == null)
                return false;

            ElementId existingTagId;
            if (createdTagIds.TryGetValue(candidate.ElementId, out existingTagId))
            {
                IndependentTag existingTag = doc.GetElement(existingTagId) as IndependentTag;
                if (existingTag != null && existingTag.IsValidObject)
                    return TryMoveExistingTag(doc, activeView, existingTag, leaderLogic, finalTarget);
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

            if (!ApplyFreshLeader(doc, activeView, newTag, leaderLogic))
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
        private static bool ApplyFreshLeader(
            Document doc, View activeView, IndependentTag tag, LeaderLogicService leaderLogic)
        {
            // PreserveLeaderEnd(useRollbackProbe: true) - no outside-text nudge and no toggle-the-
            // leader-condition fallback, matching Rearrange Tags' deliberate choice, but the rollback
            // probe IS kept because a freshly created tag's leader end is not always readable straight
            // away. That is a Revit read quirk, not a style choice.
            return TagLeaderService.ApplyLShapedLeader(
                doc, tag, activeView, leaderLogic, TagLeaderOptions.PreserveLeaderEnd(true));
        }

        /// <summary>
        /// Moves an already-created tag to a new head position and re-computes its L-shaped elbow -
        /// same technique as Rearrange Tags' own TryMoveTag, reading L1 fresh each time since nothing
        /// else touches this tag's leader end between our own clicks.
        /// </summary>
        private static bool TryMoveExistingTag(
            Document doc,
            View activeView,
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

            // PreserveLeaderEnd(useRollbackProbe: false) - this tag already existed and nothing else
            // touches its leader end between our own clicks, so the probe is not needed here.
            // EnableLeaderIfMissing is switched OFF to match exactly what this method did before: it
            // only ever READ HasLeader and returned early when the leader was off, it never turned one
            // on. In practice these tags are all created with a leader, but keeping it faithful means
            // a leader somebody deliberately switched off stays off.
            // finalTarget is the head fallback for the case where the tag will not report its own
            // position after the move.
            TagLeaderOptions options = TagLeaderOptions.PreserveLeaderEnd(false);
            options.EnableLeaderIfMissing = false;

            return TagLeaderService.ApplyLShapedLeader(
                doc, tag, activeView, leaderLogic, options,
                leaderEndOverride: null,
                headFallback: finalTarget);
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
