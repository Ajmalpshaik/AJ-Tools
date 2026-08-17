// Tool Name: Create Tags Service
// Description: Select MEP elements - already selected, or picked when the tool asks - and every one
//              is tagged straight away at the distance set in Create Tags Settings. Skips elements
//              configured minimum length, or a vertical run - same spirit as Smart MEP Tag's own
//              filters, reused where identical and widened where Ajmal asked for it (vertical-run
//              skip covers duct, pipe, AND cable tray here - Smart MEP Tag itself only checks ducts).
//              Eligibility/matching logic lives in CreateTagsEligibilityFilter, shared with StackTagsService.
// Author: Ajmal P.S.
// Version: 1.1.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Models.SmartTag, AJTools.Models.CreateTags,
//               AJTools.Services.SmartTag, AJTools.Services.LeaderLogic, AJTools.Utils

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using Autodesk.Revit.UI.Selection;
using AJTools.Services.TagClash;
using AJTools.Models.CreateTags;
using AJTools.Models.SmartTag;
using AJTools.Services.LeaderLogic;
using AJTools.Services.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.CreateTags
{
    /// <summary>
    /// Orchestrates the Create Tags pipeline: validate view -> filter the current selection down to
    /// eligible elements -> resolve a tag family for each -> let the user click a location per
    /// element (nearest untagged-in-this-run element wins each click) -> place the tag with a clean
    /// L-shaped leader back to the element.
    /// </summary>
    internal static class CreateTagsService
    {
        private const string ToolTitle = "Create Tags";

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

            // Selection first, tagging afterwards - never mid-selection (Ajmal, 2026-08-17).
            // Already selected before pressing the button? Tag that. Nothing selected? Hand it to
            // Revit's own picker, where he can single-click, ctrl+click, or drag a crossing window,
            // and nothing happens until he presses Finish. PickObjectS, not PickObject in a loop:
            // the loop form would tag each element the instant it was clicked, which is exactly what
            // he said he does not want.
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

            if (selectedIds == null || selectedIds.Count == 0)
            {
                try
                {
                    IList<Reference> picked = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        "Select the elements to tag - click, ctrl+click or drag a window, then press Finish");

                    selectedIds = picked == null
                        ? new List<ElementId>()
                        : picked.Where(r => r != null).Select(r => r.ElementId).ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // Esc before finishing - nothing selected, nothing to report.
                    return Result.Cancelled;
                }
            }

            if (selectedIds == null || selectedIds.Count == 0)
                return Result.Cancelled;

            var tracker = new CreateTagsSettingsTracker(doc);
            CreateTagsSettingsState settings = CreateTagsSettingsTracker.EnsureDefaults(tracker.LastState);
            double minLengthInternal = CreateTagsSettingsTracker.ResolveMinLengthInternal(settings);

            var tally = new SkipTally();
            HashSet<ElementId> alreadyTagged = SmartMepTagService.CollectAlreadyTaggedElementIds(doc, activeView);

            List<TagCandidate> candidates = CreateTagsEligibilityFilter.BuildEligibleCandidates(
                doc, activeView, selectedIds, settings, minLengthInternal, alreadyTagged, tally);

            if (candidates.Count == 0)
            {
                ShowNothingToTag(tally);
                return Result.Cancelled;
            }

            var familyResults = new List<TagPlacementResult>();
            List<string> tagWarnings = SmartMepTagService.SelectTagFamilies(doc, preflight, candidates, familyResults);
            foreach (TagPlacementResult result in familyResults)
                tally.Add(string.Format("No tag family loaded for {0}", SmartTagSettingsTracker.GetCategoryLabel(result.Category)));

            if (candidates.Count == 0)
            {
                ShowNothingToTag(tally);
                return Result.Cancelled;
            }

            int totalEligible = candidates.Count;
            int placedCount = 0;
            bool hadCommit = false;
            LeaderLogicService leaderLogic = new LeaderLogicService(activeView);
            List<TagCandidate> remaining = new List<TagCandidate>(candidates);

            using (TransactionGroup tg = new TransactionGroup(doc, "Create Tags"))
            {
                tg.Start();

                // No clicking for positions any more (changed 2026-08-17). Every tag is placed
                // straight away at the offset from its own element, using the same side rule the
                // tagging tools already follow and the same L-shaped leader routine as before.
                double offsetFeet = ResolveTagOffsetFeet(activeView, settings);
                XYZ viewRight = TagViewGeometry.GetViewRight(activeView);
                XYZ viewUp = TagViewGeometry.GetViewUp(activeView);

                foreach (TagCandidate candidate in remaining)
                {
                    XYZ tagPoint = ResolveAutomaticTagPoint(candidate, offsetFeet, viewRight, viewUp);
                    if (tagPoint == null)
                    {
                        tally.Add("Could not work out where to put this tag");
                        continue;
                    }

                    using (Transaction t = new Transaction(doc, "Create Tag"))
                    {
                        t.Start();
                        bool placed;
                        try
                        {
                            placed = TryCreateTagAt(doc, activeView, leaderLogic, candidate, tagPoint);
                        }
                        catch (Exception)
                        {
                            placed = false;
                        }

                        if (placed)
                        {
                            t.Commit();
                            hadCommit = true;
                            placedCount++;
                        }
                        else
                        {
                            t.RollBack();
                            tally.Add("Could not place a tag for this element");
                        }
                    }
                }

                remaining.Clear();

                if (hadCommit)
                    tg.Assimilate();
                else
                    tg.RollBack();
            }

            ShowRunSummary(placedCount, tally, tagWarnings);

            return placedCount > 0 ? Result.Succeeded : Result.Cancelled;
        }

        /// <summary>
        /// How far a tag sits from its element, in feet, from the user's mm-on-paper setting.
        /// </summary>
        /// <remarks>
        /// Scaled by the view, per the standing rule that every mm clearance is computed against
        /// view.Scale rather than hardcoded - a distance tuned at one scale is wrong at every other.
        /// </remarks>
        private static double ResolveTagOffsetFeet(View view, CreateTagsSettingsState settings)
        {
            double offsetMm = settings != null && settings.TagOffsetMm > 0.0
                ? settings.TagOffsetMm
                : CreateTagsSettingsTracker.DefaultTagOffsetMm;

            int viewScale = TagViewGeometry.GetViewScale(view);
            return offsetMm * Constants.MM_TO_FEET * viewScale;
        }

        /// <summary>
        /// Works out where a tag goes without asking the user to click for it.
        /// </summary>
        /// <remarks>
        /// Follows the side rule already recorded for this project: a run lying horizontally in the
        /// view is tagged BELOW it, and a run lying vertically is tagged to its RIGHT. That rule
        /// exists so two mirrored branches of the same size end up tagged the same way instead of one
        /// above and one below - consistency by orientation, never alternating by placement order.
        /// Anything that is not a run (equipment, an accessory) has no direction to read, so it takes
        /// the same treatment as a horizontal one.
        /// </remarks>
        private static XYZ ResolveAutomaticTagPoint(
            TagCandidate candidate, double offsetFeet, XYZ viewRight, XYZ viewUp)
        {
            if (candidate == null || candidate.Midpoint == null || viewRight == null || viewUp == null)
                return null;

            XYZ direction = candidate.Orientation == ElementOrientation.Vertical
                ? viewRight
                : viewUp.Negate();

            return candidate.Midpoint.Add(direction.Multiply(offsetFeet));
        }

        /// <summary>
        /// Creates the tag at the given point for the candidate's element, sets its resolved tag type,
        /// and attaches a clean L-shaped leader back to the element (reusing SmartTagPlacementEngine's
        /// leader routine - same technique Smart MEP Tag itself uses for freshly created tags).
        /// </summary>
        private static bool TryCreateTagAt(
            Document doc,
            View activeView,
            LeaderLogicService leaderLogic,
            TagCandidate candidate,
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

            return SmartTagPlacementEngine.ApplyLeaderBehavior(doc, newTag, activeView, leaderLogic);
        }

        private static void ShowNothingToTag(SkipTally tally)
        {
            string summary = tally.BuildSummary();
            string body = summary != null
                ? "Nothing to tag from your selection.\n\n" + summary
                : "Nothing to tag from your selection.";
            DialogHelper.ShowInfo(ToolTitle, body);
        }

        private static void ShowRunSummary(int placedCount, SkipTally tally, List<string> tagWarnings)
        {
            string summary = tally.BuildSummary();
            string body = string.Format("{0} tag(s) created.", placedCount);
            if (summary != null)
                body += "\n\nSkipped:\n" + summary;

            if (tagWarnings != null && tagWarnings.Count > 0)
                body += "\n\nNote:\n- " + string.Join("\n- ", tagWarnings);

            DialogHelper.ShowInfo(ToolTitle, body);
        }
    }
}
