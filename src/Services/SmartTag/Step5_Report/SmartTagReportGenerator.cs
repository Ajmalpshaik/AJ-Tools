// Tool Name: Smart Tag Report Generator
// Description: Step 5 — the report after all tagging is complete, and the "nothing to tag" message.
// Author: Ajmal P.S.
// Version: 1.2.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models.SmartTag
// Changelog: 1.2.0 (2026-10-15) - The "No elements to tag" message moved here from the run
//            (BuildNothingToTagMessage), unchanged, so every message the tool shows lives in one file.
//            1.1.0 (2026-10-14) - Linked models: one line says how many linked elements were found and
//            tagged and how many links were read (and when the view was estimated, on Revit 2020-2022).
//            A linked element in the manual-review list names its link, since its id is the link's.
//            1.0.1 (2026-10-12) - Vertical/minor items get their own line and are counted in Filtered Out,
//            a tag Revit refused to create is listed as refused, and the session-telemetry block is gone.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Generates the final output report displayed to the user after Smart MEP Tagging completes.
    /// Shows a clear breakdown of what was tagged, skipped, and what needs manual review.
    /// </summary>
    internal static class SmartTagReportGenerator
    {
        /// <summary>
        /// Builds and displays the final report using a TaskDialog.
        /// Shows counts per outcome and lists element IDs that need manual review.
        /// </summary>
        /// <param name="linkedScan">The linked-model scan, or null when linked elements were not included.</param>
        public static void ShowReport(
            PreFlightResult preflight,
            List<TagPlacementResult> results,
            List<string> tagWarnings,
            SmartTagLinkedScan linkedScan = null)
        {
            if (preflight == null || preflight.ActiveView == null || results == null)
            {
                DialogHelper.ShowInfo("Smart MEP Tag", "Tagging completed, but report data was not available.");
                return;
            }

            // Every result lands on exactly one line below, so the lines add up to the total. Two
            // used to go missing or be misfiled: vertical runs and minor accessories (FilteredOutType)
            // were counted in the total but shown on no line, and a tag Revit refused to create was
            // filed under "No Clean Space". A failed result with no skip reason is that refusal.
            int totalAnalysed = results.Count;
            int successCount = results.Count(r => r.Success);
            int alreadyTagged = results.Count(r => r.SkipReason == TagSkipReason.AlreadyTagged);
            int filteredSize = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutSize);
            int filteredType = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutType);
            int filteredVisibility = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutVisibility);
            int outsideCrop = results.Count(r => r.SkipReason == TagSkipReason.OutsideCropRegion);
            int denseSkipped = results.Count(r => r.SkipReason == TagSkipReason.DenseZoneSkipped);
            int groupSkipped = results.Count(r => r.SkipReason == TagSkipReason.PartOfTaggedGroup);
            int noCleanSpace = results.Count(r => r.SkipReason == TagSkipReason.NoCleanSpaceAvailable);
            int placementRefused = results.Count(IsPlacementRefused);
            int noTagFamily = results.Count(r => r.SkipReason == TagSkipReason.NoTagFamilyAvailable);
            int filteredTotal = filteredSize + filteredType + filteredVisibility + outsideCrop;

            var sb = new StringBuilder();

            // ── Summary header ──
            sb.AppendLine(string.Format("View Name:    {0}", preflight.ActiveView.Name));
            sb.AppendLine(string.Format("View Scale:   1:{0}", preflight.ViewScale));
            sb.AppendLine(string.Format("View Type:    {0}", preflight.ViewType));
            sb.AppendLine();

            // ── Counts ──
            sb.AppendLine(string.Format("Total Elements Analysed:     {0}", totalAnalysed));
            sb.AppendLine(string.Format("Successfully Tagged:         {0}", successCount));
            sb.AppendLine(string.Format("Already Tagged (skipped):    {0}", alreadyTagged));
            sb.AppendLine(string.Format("Filtered Out:                {0}", filteredTotal));
            if (filteredSize > 0)
                sb.AppendLine(string.Format("    - Below size threshold:  {0}", filteredSize));
            if (filteredType > 0)
                sb.AppendLine(string.Format("    - Vertical/minor items:  {0}", filteredType));
            if (filteredVisibility > 0)
                sb.AppendLine(string.Format("    - Hidden in view:        {0}", filteredVisibility));
            if (outsideCrop > 0)
                sb.AppendLine(string.Format("    - Outside crop region:   {0}", outsideCrop));
            sb.AppendLine(string.Format("Skipped (Dense Zone):        {0}", denseSkipped));
            sb.AppendLine(string.Format("Skipped (Tagged Group):      {0}", groupSkipped));
            sb.AppendLine(string.Format("Failed (No Clean Space):     {0}", noCleanSpace));
            if (placementRefused > 0)
                sb.AppendLine(string.Format("Failed (Revit Refused Tag):  {0}", placementRefused));
            sb.AppendLine(string.Format("No Tag Family Found:         {0}", noTagFamily));

            // ── Linked models ──
            string linkedLine = DescribeLinkedScan(linkedScan, results);
            if (!string.IsNullOrEmpty(linkedLine))
            {
                sb.AppendLine();
                sb.AppendLine(linkedLine);
            }

            // ── Tag warnings ──
            if (tagWarnings != null && tagWarnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("TAG FAMILY WARNINGS:");
                foreach (string warning in tagWarnings)
                    sb.AppendLine(string.Format("  • {0}", warning));
            }

            // ── Manual review list ──
            List<TagPlacementResult> manualReview = results
                .Where(r => r.SkipReason == TagSkipReason.NoCleanSpaceAvailable || IsPlacementRefused(r))
                .ToList();

            if (manualReview.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("MANUAL REVIEW REQUIRED:");
                sb.AppendLine("The following elements could not be tagged automatically.");
                sb.AppendLine("Please tag them manually:");
                sb.AppendLine();

                foreach (TagPlacementResult r in manualReview)
                {
                    string catName = GetCategoryDisplayName(r.Category);
                    // A linked element's id is its id inside the link, so say which link.
                    sb.AppendLine(string.Format("  Element ID: {0}  ({1}){2}{3}",
                        r.ElementId.IntValue(),
                        catName,
                        r.IsLinked ? " in link " + r.LinkName : "",
                        string.IsNullOrEmpty(r.Note) ? "" : " — " + r.Note));
                }
            }

            // ── Display ──
            string title = successCount > 0
                ? string.Format("Smart MEP Tag — {0} Tags Placed", successCount)
                : "Smart MEP Tag — Complete (No Tags Placed)";

            DialogHelper.ShowDialog(title, GetMainInstruction(successCount, totalAnalysed), sb.ToString());
        }

        /// <summary>
        /// The message shown when nothing in the view can be tagged: how many elements were looked at,
        /// how many each skip rule took out, and the single most common reason.
        /// </summary>
        internal static string BuildNothingToTagMessage(
            List<TagPlacementResult> results,
            SmartTagLinkedScan linkedScan)
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
            string linkedLine = DescribeLinkedScan(linkedScan, results);
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

            return summary.ToString();
        }
        /// <summary>
        /// One plain line about the linked models, or null when linked elements were not included.
        /// Shared with the "nothing to tag" message so both say the same thing.
        /// </summary>
        internal static string DescribeLinkedScan(SmartTagLinkedScan linkedScan, IList<TagPlacementResult> results)
        {
            if (linkedScan == null)
                return null;

            if (linkedScan.LinksInView == 0)
                return "Linked models: none loaded and shown in this view, so only this model was tagged.";

            int linkedAnalysed = results == null ? 0 : results.Count(r => r != null && r.IsLinked);
            int linkedTagged = results == null ? 0 : results.Count(r => r != null && r.IsLinked && r.Success);

            string line = string.Format(
                "Linked models: {0} tagged of {1} linked elements found, across {2} link{3} shown in this view.",
                linkedTagged,
                linkedAnalysed,
                linkedScan.LinksInView,
                linkedScan.LinksInView == 1 ? "" : "s");

            if (linkedScan.UsedViewEstimate)
            {
                line += "\nThe linked elements in this view were worked out from the crop region and view "
                      + "range (this Revit version cannot list them exactly) - an element hidden one by one "
                      + "or by a view filter inside a link may still have been tagged.";
            }

            return line;
        }

        /// <summary>
        /// A tag that had a clean spot but that Revit refused to create there. The placement engine
        /// records it as failed with no skip reason - TagSkipReason has no value for it.
        /// </summary>
        private static bool IsPlacementRefused(TagPlacementResult result)
        {
            return result != null && !result.Success && result.SkipReason == TagSkipReason.None;
        }

        /// <summary>
        /// Returns a short main instruction line based on results.
        /// </summary>
        private static string GetMainInstruction(int success, int total)
        {
            if (total == 0)
                return "No MEP elements found in this view.";
            if (success == 0)
                return "No tags could be placed. See details below.";
            if (success == total)
                return "All elements tagged successfully.";
            return string.Format("{0} of {1} elements tagged.", success, total);
        }

        /// <summary>
        /// Returns a readable display name for a BuiltInCategory.
        /// </summary>
        private static string GetCategoryDisplayName(BuiltInCategory bic)
        {
            switch (bic)
            {
                case BuiltInCategory.OST_DuctCurves: return "Duct";
                case BuiltInCategory.OST_PipeCurves: return "Pipe";
                case BuiltInCategory.OST_MechanicalEquipment: return "Mechanical Equipment";
                case BuiltInCategory.OST_PipeAccessory: return "Pipe Accessory";
                case BuiltInCategory.OST_DuctAccessory: return "Duct Accessory";
                case BuiltInCategory.OST_CableTray: return "Cable Tray";
                default: return bic.ToString();
            }
        }
    }
}
