#region Metadata
/*
 * Tool Name     : Fix Tag Clash
 * File Name     : CmdFixTagClash.cs
 * Purpose       : Finds every clashing tag in the active view and separates them, then colours
 *                 whatever could not be separated so it can be sorted out by hand.
 *
 *                 This is the "place first, fix after" idea as its own button: it does not care how
 *                 the tags got there. Smart MEP Tags, Create Tags, Stack Tags, Revit's own Tag All,
 *                 or tags placed by hand - run this afterwards and it cleans them up. Run it again
 *                 and it picks up where it left off, so a view that needs more than one round of
 *                 passes can simply be run twice.
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
 * Dependencies  : Autodesk Revit API, AJTools.Services.TagClash (TagClashEngine, TagClashHighlighter,
 *                 TagViewGeometry), AJTools.Utils (TagClashSettings, DialogHelper, ValidationHelper)
 *
 * Input         : Active View - every tag in it.
 * Output        : Tags moved apart; unfixable ones coloured, selected, and counted in the report.
 *
 * Notes         :
 * - The whole run is ONE transaction, so a single Undo puts the view back exactly as it was.
 * - Pinned tags are never moved. Tags clashing with a text note or dimension always give way,
 *   because the annotation cannot be moved for them.
 * - Number of passes, drift limit, mark colour and the clash tolerances all come from
 *   Fix Tag Clash Settings.
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.SmartTag;
using AJTools.Services.TagClash;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Separates clashing tags in the active view and marks whatever could not be separated.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdFixTagClash : IExternalCommand
    {
        private const string ToolTitle = "Fix Tag Clash";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
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
                if (activeView == null || activeView.IsTemplate || activeView is ViewSheet)
                {
                    DialogHelper.ShowError(
                        ToolTitle,
                        "Please run this tool in a normal project view, not a sheet or a view template.");
                    return Result.Cancelled;
                }

                TagClashSettingsState settings = TagClashSettings.Load();

                XYZ viewRight = TagViewGeometry.GetViewRight(activeView);
                XYZ viewUp = TagViewGeometry.GetViewUp(activeView);

                List<TagClashItem> items = TagClashEngine.CollectMovableTags(doc, activeView, viewRight, viewUp);
                if (items.Count == 0)
                {
                    DialogHelper.ShowInfo(ToolTitle, "No tags were found in the active view.");
                    return Result.Cancelled;
                }

                // Ask before a long run. Once the transaction starts, Revit is busy and there is nothing
                // to press - the tool has no window, so no progress bar and no Cancel. Saying what is
                // about to happen is the honest alternative to a silent freeze.
                if (items.Count > TagClashSettings.LongRunConfirmThreshold
                    && !DialogHelper.ShowYesNo(
                        ToolTitle,
                        string.Format(
                            "About to check {0} tags in this view, over up to {1} round(s).\n\n"
                            + "Revit will be busy while it works and can't be stopped part way. It is a "
                            + "single undo step, so you can undo the whole thing afterwards.\n\n"
                            + "Continue?",
                            items.Count, settings.FixPasses)))
                {
                    return Result.Cancelled;
                }

                List<AnnotationBox> obstacles = TagClashEngine.CollectStaticObstacles(
                    doc, activeView, viewRight, viewUp);

                TagClashRunResult run;
                int marked = 0;

                using (Transaction transaction = new Transaction(doc, "Fix Tag Clash"))
                {
                    transaction.Start();

                    try
                    {
                        run = TagClashEngine.Run(doc, activeView, settings, items, obstacles);

                        if (settings.MarkFailures && run.UnfixedTagIds.Count > 0)
                        {
                            marked = TagClashHighlighter.Mark(
                                doc, activeView, run.UnfixedTagIds, settings.MarkColour);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (transaction.HasStarted() && !transaction.HasEnded())
                            transaction.RollBack();

                        message = ex.Message;
                        DialogHelper.ShowError(ToolTitle, "The run failed and nothing was changed.\n\n" + ex.Message);
                        return Result.Failed;
                    }

                    if (run.MovesApplied > 0 || marked > 0)
                        transaction.Commit();
                    else
                        transaction.RollBack();
                }

                TrySelectUnfixed(uidoc, run.UnfixedTagIds);
                ShowReport(activeView, settings, run, marked);

                return run.MovesApplied > 0 || run.ClashingAtStart == 0
                    ? Result.Succeeded
                    : Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Leaves the unfixed tags selected so they can be stepped through straight away.
        /// </summary>
        private static void TrySelectUnfixed(UIDocument uidoc, IList<ElementId> unfixed)
        {
            if (uidoc == null || unfixed == null || unfixed.Count == 0)
                return;

            try
            {
                uidoc.Selection.SetElementIds(new List<ElementId>(unfixed));
            }
            catch (Exception)
            {
                // Selection is a convenience only - never fail the run over it.
            }
        }

        private static void ShowReport(
            View view, TagClashSettingsState settings, TagClashRunResult run, int marked)
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format("View:              {0}", view.Name));
            sb.AppendLine(string.Format("View Scale:        1:{0}", TagViewGeometry.GetViewScale(view)));
            sb.AppendLine();
            sb.AppendLine(string.Format("Tags in view:      {0}", run.TotalTags));
            sb.AppendLine(string.Format("Clashing at start: {0}", run.ClashingAtStart));
            sb.AppendLine(string.Format("Separated:         {0}", run.Fixed));
            sb.AppendLine(string.Format("Still clashing:    {0}", run.StillClashing));
            sb.AppendLine();
            sb.AppendLine(string.Format("Rounds used:       {0} of {1}", run.PassesUsed, settings.FixPasses));
            sb.AppendLine(string.Format("Tags moved:        {0}", run.MovesApplied));

            if (run.SkippedPinned > 0)
                sb.AppendLine(string.Format("Pinned, left alone: {0}", run.SkippedPinned));

            if (run.BlockedByDriftLimit > 0)
            {
                sb.AppendLine(string.Format(
                    "Moves refused for going past the {0:F0}mm drift limit: {1}",
                    settings.MaxDriftMm, run.BlockedByDriftLimit));
            }

            if (run.StillClashing > 0)
            {
                sb.AppendLine();
                if (settings.MarkFailures && marked > 0)
                {
                    sb.AppendLine(string.Format(
                        "{0} tag(s) are coloured {1} and left selected.",
                        marked, settings.MarkColour.ToString().ToLowerInvariant()));
                    sb.AppendLine("Use Clear Tag Clash Marks once you have fixed them by hand.");
                }
                else if (settings.MarkFailures)
                {
                    sb.AppendLine("The colour could not be applied to these tags, but they are left selected.");
                }
                else
                {
                    sb.AppendLine("Colouring is switched off in settings. The tags are left selected.");
                }

                sb.AppendLine();
                sb.AppendLine("Running the tool again will have another go at them.");
            }

            string title = run.ClashingAtStart == 0
                ? ToolTitle + " - Nothing Clashing"
                : string.Format("{0} - {1} Separated", ToolTitle, run.Fixed);

            DialogHelper.ShowDialog(title, BuildInstruction(run), sb.ToString());
        }

        private static string BuildInstruction(TagClashRunResult run)
        {
            if (run.ClashingAtStart == 0)
                return "No clashing tags found in this view.";

            if (run.StillClashing == 0)
                return string.Format("All {0} clashing tag(s) separated.", run.ClashingAtStart);

            return string.Format(
                "{0} of {1} separated. {2} still need attention.",
                run.Fixed, run.ClashingAtStart, run.StillClashing);
        }
    }
}
