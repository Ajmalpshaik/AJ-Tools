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

            // Two ways to work, decided by whether anything was selected before the button was
            // pressed (Ajmal, 2026-08-17):
            //   BATCH     - elements already selected: every one of them is tagged in one go.
            //   ONE BY ONE - nothing selected: click an element and it is tagged immediately, click
            //                the next, Esc to finish. Deliberately PickObject (single) in a loop and
            //                NOT PickObjects with a Finish button - he asked for the tag to appear on
            //                the click, not for a multi-selection to gather first.
            ICollection<ElementId> preSelectedIds = uidoc.Selection.GetElementIds();
            bool oneByOne = preSelectedIds == null || preSelectedIds.Count == 0;

            var tracker = new CreateTagsSettingsTracker(doc);
            CreateTagsSettingsState settings = CreateTagsSettingsTracker.EnsureDefaults(tracker.LastState);
            double minLengthInternal = CreateTagsSettingsTracker.ResolveMinLengthInternal(settings);

            var tally = new SkipTally();
            HashSet<ElementId> alreadyTagged = SmartMepTagService.CollectAlreadyTaggedElementIds(doc, activeView);

            int placedCount = 0;
            bool hadCommit = false;
            var tagWarnings = new List<string>();
            LeaderLogicService leaderLogic = new LeaderLogicService(activeView);

            // No clicking for POSITIONS in either mode (changed 2026-08-17). Every tag lands at the
            // offset from its own element, on the side the project's rule specifies, with the same
            // L-shaped leader routine as before.
            double offsetFeet = ResolveTagOffsetFeet(settings);
            XYZ viewRight = TagViewGeometry.GetViewRight(activeView);
            XYZ viewUp = TagViewGeometry.GetViewUp(activeView);

            using (TransactionGroup tg = new TransactionGroup(doc, "Create Tags"))
            {
                tg.Start();

                if (!oneByOne)
                {
                    placedCount += TagElements(
                        doc, activeView, preflight, settings, minLengthInternal, preSelectedIds,
                        alreadyTagged, leaderLogic, offsetFeet, viewRight, viewUp, tally, tagWarnings,
                        ref hadCommit);
                }
                else
                {
                    // One element per click, tagged on the spot. The picker reopens until Esc, and
                    // alreadyTagged grows as we go, so clicking the same element twice is reported as
                    // already tagged rather than stacking a second tag on it.
                    var single = new List<ElementId>(1) { ElementId.InvalidElementId };

                    while (true)
                    {
                        Reference picked;
                        try
                        {
                            picked = uidoc.Selection.PickObject(
                                ObjectType.Element,
                                "Click an element to tag it - Esc to finish");
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            break;
                        }

                        if (picked == null)
                            break;

                        single[0] = picked.ElementId;

                        placedCount += TagElements(
                            doc, activeView, preflight, settings, minLengthInternal, single,
                            alreadyTagged, leaderLogic, offsetFeet, viewRight, viewUp, tally,
                            tagWarnings, ref hadCommit);
                    }
                }

                if (hadCommit)
                    tg.Assimilate();
                else
                    tg.RollBack();
            }

            ShowRunSummary(placedCount, tally, tagWarnings);

            return placedCount > 0 ? Result.Succeeded : Result.Cancelled;
        }

        /// <summary>
        /// Filters a set of element ids down to what can actually be tagged, resolves a tag family
        /// for each, and places every tag. Returns how many went in.
        /// </summary>
        /// <remarks>
        /// One routine for both ways of working: the batch path calls it once with everything that was
        /// selected, the one-by-one path calls it once per click with a single id. Keeping them on the
        /// same routine is what stops the two modes drifting into different skip rules - the exact
        /// drift that had to be undone across the tag tools in v1.49.1.
        ///
        /// <paramref name="alreadyTagged"/> is updated in place as tags go in, so in one-by-one mode
        /// clicking the same element twice is reported as already tagged instead of stacking a second
        /// tag on top of the first.
        /// </remarks>
        private static int TagElements(
            Document doc,
            View activeView,
            PreFlightResult preflight,
            CreateTagsSettingsState settings,
            double minLengthInternal,
            ICollection<ElementId> elementIds,
            HashSet<ElementId> alreadyTagged,
            LeaderLogicService leaderLogic,
            double offsetFeet,
            XYZ viewRight,
            XYZ viewUp,
            SkipTally tally,
            List<string> tagWarnings,
            ref bool hadCommit)
        {
            List<TagCandidate> candidates = CreateTagsEligibilityFilter.BuildEligibleCandidates(
                doc, activeView, elementIds, settings, minLengthInternal, alreadyTagged, tally);

            if (candidates.Count == 0)
                return 0;

            var familyResults = new List<TagPlacementResult>();
            List<string> warnings = SmartMepTagService.SelectTagFamilies(
                doc, preflight, candidates, familyResults);

            foreach (TagPlacementResult result in familyResults)
            {
                tally.Add(string.Format(
                    "No tag family loaded for {0}",
                    SmartTagSettingsTracker.GetCategoryLabel(result.Category)));
            }

            if (warnings != null)
            {
                foreach (string warning in warnings)
                {
                    // One-by-one mode calls this on every click, so the same warning would otherwise
                    // be repeated once per element in the end-of-run summary.
                    if (!tagWarnings.Contains(warning))
                        tagWarnings.Add(warning);
                }
            }

            int placed = 0;

            foreach (TagCandidate candidate in candidates)
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
                    bool ok;
                    try
                    {
                        ok = TryCreateTagAt(doc, activeView, leaderLogic, candidate, tagPoint);
                    }
                    catch (Exception)
                    {
                        ok = false;
                    }

                    if (ok)
                    {
                        t.Commit();
                        hadCommit = true;
                        placed++;
                        alreadyTagged.Add(candidate.ElementId);
                    }
                    else
                    {
                        t.RollBack();
                        tally.Add("Could not place a tag for this element");
                    }
                }
            }

            return placed;
        }

        /// <summary>
        /// How far a tag sits from its element, in feet. A REAL distance in the model, not a size on
        /// the printed sheet.
        /// </summary>
        /// <remarks>
        /// NOT multiplied by the view scale, and that is deliberate - it was, in the first build of
        /// this, and it put every tag scale-times too far away: at 1:100 a 300mm setting threw the tag
        /// 30 METRES off its duct. The 300mm default was taken from Smart MEP Tags, and that tool's
        /// offset is a model distance (SmartTagSettingsTracker.ResolveOffsetInternal is
        /// <c>mm * MM_TO_FEET</c> with no scale term). Copying its number while changing its meaning
        /// is what broke it.
        ///
        /// This is a real exception to the "check view.Scale first" rule, which is about clearances
        /// measured on the SHEET - tag-to-tag gaps, elbow pushes, clash margins. A tag sitting 300mm
        /// off a duct is a distance in the building, and it should stay 300mm whether the sheet is
        /// 1:50 or 1:100.
        /// </remarks>
        private static double ResolveTagOffsetFeet(CreateTagsSettingsState settings)
        {
            double offsetMm = settings != null && settings.TagOffsetMm > 0.0
                ? settings.TagOffsetMm
                : CreateTagsSettingsTracker.DefaultTagOffsetMm;

            return offsetMm * Constants.MM_TO_FEET;
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

            // Measured from the element's EDGE, not its centre. Going from the centre means the
            // distance the user typed is eaten by half the duct before it clears anything - on a wide
            // duct the tag lands inside it. Same shape as the placement engine's own
            // "hostHalfW + offsetFromHostToTextEdge".
            double hostHalf = Math.Max(0.0, candidate.HostWidth) * 0.5;

            return candidate.Midpoint.Add(direction.Multiply(hostHalf + offsetFeet));
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

        /// <summary>
        /// Reports the run - but only when there is something to report.
        /// </summary>
        /// <remarks>
        /// House rule: a tool that did everything asked of it finishes SILENTLY (the model is the
        /// result), a tool that did nothing must say why, and a tool that did only part of it says
        /// what it left out. So the report is gated on "was anything skipped or warned about", not on
        /// "did the tool run".
        ///
        /// This matters more since tagging went one-element-per-click: that mode ends on Esc, and
        /// popping up "0 tag(s) created" every time someone finishes with Esc - or after a clean run
        /// where every tag went in - is exactly the confirming-the-obvious dialog the rule exists to
        /// stop.
        /// </remarks>
        private static void ShowRunSummary(int placedCount, SkipTally tally, List<string> tagWarnings)
        {
            string summary = tally?.BuildSummary();
            bool hasWarnings = tagWarnings != null && tagWarnings.Count > 0;

            if (summary == null && !hasWarnings)
                return;

            string body = string.Format("{0} tag(s) created.", placedCount);

            if (summary != null)
                body += "\n\nSkipped:\n" + summary;

            if (hasWarnings)
                body += "\n\nNote:\n- " + string.Join("\n- ", tagWarnings);

            DialogHelper.ShowInfo(ToolTitle, body);
        }
    }
}
