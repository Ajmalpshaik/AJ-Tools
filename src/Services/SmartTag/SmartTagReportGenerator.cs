// Tool Name: Smart Tag Report Generator
// Description: Phase 7 â€” generates a clean formatted report after all tagging is complete.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models.SmartTag

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
        public static void ShowReport(
            PreFlightResult preflight,
            List<TagPlacementResult> results,
            List<string> tagWarnings)
        {
            if (preflight == null || preflight.ActiveView == null || results == null)
            {
                DialogHelper.ShowInfo("Smart MEP Tag", "Tagging completed, but report data was not available.");
                return;
            }

            int totalAnalysed = results.Count;
            int successCount = results.Count(r => r.Success);
            int alreadyTagged = results.Count(r => r.SkipReason == TagSkipReason.AlreadyTagged);
            int filteredSize = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutSize);
            int filteredVisibility = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutVisibility);
            int outsideCrop = results.Count(r => r.SkipReason == TagSkipReason.OutsideCropRegion);
            int filteredType = results.Count(r => r.SkipReason == TagSkipReason.FilteredOutType);
            int denseSkipped = results.Count(r => r.SkipReason == TagSkipReason.DenseZoneSkipped);
            int groupSkipped = results.Count(r => r.SkipReason == TagSkipReason.PartOfTaggedGroup);
            int noCleanSpace = results.Count(r => r.SkipReason == TagSkipReason.NoCleanSpaceAvailable);
            int noTagFamily = results.Count(r => r.SkipReason == TagSkipReason.NoTagFamilyAvailable);
            int filteredTotal = filteredSize + filteredVisibility + outsideCrop + filteredType;

            var sb = new StringBuilder();

            // â”€â”€ Summary header â”€â”€
            sb.AppendLine(string.Format("View Name:    {0}", preflight.ActiveView.Name));
            sb.AppendLine(string.Format("View Scale:   1:{0}", preflight.ViewScale));
            sb.AppendLine(string.Format("View Type:    {0}", preflight.ViewType));
            sb.AppendLine();

            // â”€â”€ Counts â”€â”€
            sb.AppendLine(string.Format("Total Elements Analysed:     {0}", totalAnalysed));
            sb.AppendLine(string.Format("Successfully Tagged:         {0}", successCount));
            sb.AppendLine(string.Format("Already Tagged (skipped):    {0}", alreadyTagged));
            sb.AppendLine(string.Format("Filtered Out (size/vis/type): {0}", filteredTotal));
            if (filteredSize > 0)
                sb.AppendLine(string.Format("    - Below size threshold:  {0}", filteredSize));
            if (filteredVisibility > 0)
                sb.AppendLine(string.Format("    - Hidden in view:        {0}", filteredVisibility));
            if (outsideCrop > 0)
                sb.AppendLine(string.Format("    - Outside crop region:   {0}", outsideCrop));
            if (filteredType > 0)
                sb.AppendLine(string.Format("    - Excluded element type: {0}", filteredType));
            sb.AppendLine(string.Format("Skipped (Dense Zone):        {0}", denseSkipped));
            sb.AppendLine(string.Format("Skipped (Tagged Group):      {0}", groupSkipped));
            sb.AppendLine(string.Format("Failed (No Clean Space):     {0}", noCleanSpace));
            sb.AppendLine(string.Format("No Tag Family Found:         {0}", noTagFamily));

            // â”€â”€ Tag warnings â”€â”€
            if (tagWarnings != null && tagWarnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("TAG FAMILY WARNINGS:");
                foreach (string warning in tagWarnings)
                    sb.AppendLine(string.Format("  â€¢ {0}", warning));
            }

            // â”€â”€ Manual review list â”€â”€
            List<TagPlacementResult> manualReview = results
                .Where(r => r.SkipReason == TagSkipReason.NoCleanSpaceAvailable)
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
                    sb.AppendLine(string.Format("  Element ID: {0}  ({1}){2}",
                        AJTools.Utils.ElementIdHelper.GetIntegerValue(r.ElementId),
                        catName,
                        string.IsNullOrEmpty(r.Note) ? "" : " â€” " + r.Note));
                }
            }

            string telemetrySummary = SmartTagTelemetryTracker.GetSessionSummary();
            if (!string.IsNullOrWhiteSpace(telemetrySummary))
            {
                sb.AppendLine();
                sb.AppendLine("SESSION TELEMETRY:");
                sb.AppendLine(telemetrySummary);
            }

            // â”€â”€ Display â”€â”€
            string title = successCount > 0
                ? string.Format("Smart MEP Tag â€” {0} Tags Placed", successCount)
                : "Smart MEP Tag â€” Complete (No Tags Placed)";

            DialogHelper.ShowDialog(title, GetMainInstruction(successCount, totalAnalysed), sb.ToString());
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
