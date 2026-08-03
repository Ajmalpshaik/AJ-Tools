// Tool Name: Create Tags Service
// Description: Select MEP elements, then click a location for each one to create a tag there.
//              Automatically skips elements that are already tagged in the view, shorter than the
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

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                DialogHelper.ShowError(ToolTitle, "Select one or more elements to tag first, then run Create Tags.");
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

                while (remaining.Count > 0)
                {
                    XYZ pickedPoint;
                    try
                    {
                        pickedPoint = uidoc.Selection.PickPoint(string.Format(
                            "Click a location for the next tag ({0} of {1} remaining) - Esc to finish",
                            remaining.Count, totalEligible));
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    TagCandidate nearest = CreateTagsEligibilityFilter.FindNearestCandidate(remaining, leaderLogic, pickedPoint);
                    if (nearest == null)
                        break;

                    using (Transaction t = new Transaction(doc, "Create Tag"))
                    {
                        t.Start();
                        bool placed;
                        try
                        {
                            placed = TryCreateTagAt(doc, activeView, leaderLogic, nearest, pickedPoint);
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

                    remaining.Remove(nearest);
                }

                if (hadCommit)
                    tg.Assimilate();
                else
                    tg.RollBack();
            }

            if (remaining.Count > 0)
                tally.Add("Not clicked before Esc");

            ShowRunSummary(placedCount, tally, tagWarnings);

            return placedCount > 0 ? Result.Succeeded : Result.Cancelled;
        }

        /// <summary>
        /// Creates the tag at the picked point for the given candidate's element, sets its resolved
        /// tag type, and attaches a clean L-shaped leader back to the element (reusing
        /// SmartTagPlacementEngine's leader routine - same technique Smart MEP Tag itself uses for
        /// freshly created tags).
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
