#region Metadata
/*
 * Tool Name     : Smart MEP Tags - The Run
 * File Name     : SmartMepTagService.cs
 * Purpose       : One Smart MEP Tags run, start to finish, as a short list of steps. Each step lives in
 *                 its own folder next to this file:
 *                   Step1_ViewCheck     - can this view be tagged?
 *                   Step2_FindElements  - which elements, after the skip rules (host and linked)
 *                   Step3_TagTypes      - which tag type each element gets
 *                   Step4_Placement     - where each tag goes (SmartTagPositionFinder) and placing it
 *                   Step5_Report        - the report at the end
 *                   Settings            - what the settings window stores
 *                 The numbers that tune the placement algorithm are all in SmartTagTuning.cs.
 *
 * Author        : Ajmal P.S.
 * Version       : 2.0.0
 *
 * Created Date  : 2026-04-20
 * Last Updated  : 2026-10-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Utils, the step files above
 *
 * Input         : The active view and the Smart MEP Tag settings.
 * Output        : Tags placed in the view and the report. One Ctrl+Z undoes the whole run.
 *
 * Notes         :
 * - Called by CmdSmartMepTag. Create Tags, Stack Tags and Fix Tag Clash reuse the step files directly.
 *
 * Changelog     :
 * v2.0.0 (2026-10-15) - Restructured (Ajmal: "make separate code for this algorithm"). This file was
 *                       2,100 lines holding every phase; it now holds only the run, and each phase is
 *                       its own file in a step folder. Routines moved unchanged - tagging results are
 *                       the same.
 * v1.2.0 (2026-10-14) - "Also tag elements in linked models" (SmartTagLinkedElementCollector).
 * v1.1.1 (2026-10-12) - Cable trays obey the width/height minimums; minor pipe accessories reported as
 *                       a filtered type; tag gap measured from every element's edge.
 * v1.1.0 (2026-08-26) - Crop filter measures the view plane only; zero-candidate dialog names the filter.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// One Smart MEP Tags run, start to finish. Each step is its own file - see the header above.
    /// </summary>
    internal static class SmartMepTagService
    {
        private const string ToolTitle = "Smart MEP Tag";

        /// <summary>
        /// Executes the full Smart MEP Tagging pipeline on the active view.
        /// Returns Result.Succeeded when at least one tag was placed, and also when there was nothing
        /// to tag (the user is told why); Result.Failed only when Revit gives no command context; and
        /// Result.Cancelled otherwise - a check that did not pass, the user stopping it, or no tag
        /// placed.
        /// </summary>
        public static Result Execute(ExternalCommandData commandData, ref string message)
        {
            if (commandData?.Application == null)
            {
                message = "Revit command context is not available.";
                DialogHelper.ShowError(ToolTitle, message);
                return Result.Failed;
            }

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
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

#if DEBUG
            SmartTagElementGeometry.RunGeometryRegressionChecksOnce();
#endif

            View activeView = doc.ActiveView;

            // ── PHASE 0: Pre-flight checks ──
            PreFlightResult preflight = SmartTagViewCheck.RunPreFlightChecks(doc, activeView);
            if (!preflight.Passed)
            {
                DialogHelper.ShowError(ToolTitle, preflight.ErrorMessage);
                message = preflight.ErrorMessage;
                return Result.Cancelled;
            }

            // Show any warnings from pre-flight.
            if (preflight.Warnings.Count > 0)
            {
                string warningText = string.Join("\n- ", preflight.Warnings);
                bool proceed = DialogHelper.ShowYesNo(
                    ToolTitle + " - Warnings",
                    string.Format("Pre-flight checks passed with warnings:\n\n- {0}\n\nDo you want to continue?", warningText));
                if (!proceed)
                    return Result.Cancelled;
            }

            // ── PHASE 1: Collect and filter MEP elements ──
            var settingsTracker = new SmartTagSettingsTracker(doc);
            SmartTagSettingsState settingsState =
                SmartTagSettingsTracker.EnsureDefaults(settingsTracker.LastState);
            bool hasEnabledCategory = SmartTagSettingsTracker.SupportedCategories
                .Any(c => SmartTagSettingsTracker.IsCategoryEnabled(settingsState, c));
            if (!hasEnabledCategory)
            {
                DialogHelper.ShowError(ToolTitle, "No category is enabled in Smart MEP Tag Settings.");
                return Result.Cancelled;
            }

            var results = new List<TagPlacementResult>();
            List<TagCandidate> candidates = SmartTagElementCollector.CollectAndFilterElements(
                doc, preflight, settingsState, results, out SmartTagLinkedScan linkedScan);
            int candidatesAfterFilter = candidates.Count;

            if (candidates.Count == 0)
            {
                // Build a summary of why no candidates were found. Every skip already carries the
                // reason that produced it, so name the filter instead of merging four of them into
                // one "Filtered Out" number - that merged number is what sent Ajmal looking at
                // already-tagged elements while Already Tagged read zero. Vertical-run skips used to
                // fall outside the total altogether, so the three lines did not even add up.
                // Same lesson as the "No grids or levels were found" fix (2026-08-15): a
                // found-nothing message has to say WHICH check rejected everything.
                int alreadyTagged = results.Count(r => r.SkipReason == TagSkipReason.AlreadyTagged);
                int outsideCrop = results.Count(r => r.SkipReason == TagSkipReason.OutsideCropRegion);
                int belowSize = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutSize);
                int notVisible = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutVisibility);
                int excludedType = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutType);

                string topReason = results
                    .Where(r => !string.IsNullOrEmpty(r.Note))
                    .GroupBy(r => r.Note)
                    .OrderByDescending(g => g.Count())
                    .Select(g => string.Format("{0} x {1}", g.Count(), g.Key))
                    .FirstOrDefault();

                var summary = new System.Text.StringBuilder();
                summary.AppendLine("No elements to tag in this view.");
                summary.AppendLine();
                summary.AppendLine(string.Format("Analysed: {0}", results.Count));
                summary.AppendLine(string.Format("Already tagged: {0}", alreadyTagged));
                summary.AppendLine(string.Format("Outside crop region: {0}", outsideCrop));
                summary.AppendLine(string.Format("Below size limits: {0}", belowSize));
                summary.AppendLine(string.Format("Hidden or no position: {0}", notVisible));
                summary.AppendLine(string.Format("Excluded type: {0}", excludedType));
                string linkedLine = SmartTagReportGenerator.DescribeLinkedScan(linkedScan, results);
                if (!string.IsNullOrEmpty(linkedLine))
                {
                    summary.AppendLine();
                    summary.AppendLine(linkedLine);
                }
                if (!string.IsNullOrEmpty(topReason))
                {
                    summary.AppendLine();
                    summary.Append(string.Format("Main reason: {0}", topReason));
                }

                DialogHelper.ShowInfo(ToolTitle, summary.ToString());
                RecordTelemetrySafe(preflight, settingsState, results, candidatesAfterFilter, candidates.Count);
                return Result.Succeeded;
            }

            // ── PHASE 2: Select tag families for each candidate ──
            List<string> tagWarnings = SmartTagTypeSelector.SelectTagFamilies(doc, preflight, candidates, results);

            if (candidates.Count == 0)
            {
                DialogHelper.ShowInfo(ToolTitle,
                    "No tag families are loaded for the MEP categories in this view.\n"
                    + "Please load the required tag families and try again.");
                RecordTelemetrySafe(preflight, settingsState, results, candidatesAfterFilter, candidates.Count);
                return Result.Succeeded;
            }

            // Ask before a long run. Once placement starts, Revit is busy and there is nothing to press -
            // this tool has no window, so no progress bar and no Cancel. Saying what is about to happen
            // is the honest alternative to a silent freeze. Wording and threshold are shared with every
            // other Tags panel tool via DialogHelper.ConfirmLongRun.
            if (!DialogHelper.ConfirmLongRun(
                    ToolTitle,
                    candidates.Count,
                    string.Format("About to place tags on {0} elements.", candidates.Count)))
            {
                return Result.Cancelled;
            }

            // ── PHASES 3–6: Score positions, detect clashes, reposition, and place tags ──
            using (TransactionGroup tg = new TransactionGroup(doc, "Smart MEP Tag All"))
            {
                tg.Start();
                SmartTagPlacementEngine.ProcessAndPlaceTags(doc, preflight, settingsState, candidates, results);
                tg.Assimilate();
            }
            RecordTelemetrySafe(preflight, settingsState, results, candidatesAfterFilter, candidates.Count);

            // ── PHASE 7: Generate output report ──
            SmartTagReportGenerator.ShowReport(preflight, results, tagWarnings, linkedScan);

            int successCount = results.Count(r => r.Success);
            return successCount > 0 ? Result.Succeeded : Result.Cancelled;
        }

        private static void RecordTelemetrySafe(
            PreFlightResult preflight,
            SmartTagSettingsState settingsState,
            IList<TagPlacementResult> results,
            int candidatesAfterFilter,
            int candidatesAfterTagTypeResolution)
        {
            try
            {
                SmartTagTelemetryTracker.RecordRun(
                    preflight,
                    settingsState,
                    results,
                    candidatesAfterFilter,
                    candidatesAfterTagTypeResolution);
            }
            catch
            {
                // Telemetry is best effort and must never block command execution.
            }
        }
    }
}
