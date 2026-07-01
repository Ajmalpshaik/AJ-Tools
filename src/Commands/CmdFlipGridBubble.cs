#region Metadata
/*
 * Tool Name     : Flip Grid / Level Bubbles
 * File Name     : CmdFlipGridBubble.cs
 * Purpose       : Toggles which end of a grid or level shows its bubble in the active view, either by
 *                 picking datums one-by-one (instant flip) or by window-selecting a batch.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2025-12-07
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (DatumSelectionFilter)
 *
 * Input         : Active View - grids/levels picked individually or by window selection (Esc to finish).
 * Output        : Bubble shown on the opposite datum end; visible immediately in the view.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Bubble visibility API (IsBubbleVisibleInView /
 *   ShowBubbleInView / HideBubbleInView) is stable across all target versions.
 * - Project-only tool; exits cleanly in the Family Editor.
 * - Works in plan, section, and elevation views.
 * - Single-pick mode commits each instant flip on its own (one undo per pick - required because a
 *   transaction cannot stay open across a pick). Window-select flips a whole rectangle in ONE undo step.
 * - Esc during a pick is a normal cancel (handled silently); normal success is silent.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.1.1 (2025-12-10) - Removed final success popup.
 * v1.2.0 (2026-06-30) - Added mandatory metadata block; window-select now flips a whole rectangle in
 *                       one undo step; Family-Editor guard; validation cancels cleanly; graceful
 *                       per-datum skip; cleanup. Flip behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Flips the bubble end of grids and levels in the active view, one-by-one or by window selection.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdFlipGridBubble : IExternalCommand
    {
        private const string Title = "Flip Grid / Level Bubbles";
        private const string TransactionName = "AJ Tools - Flip Grid / Level Bubble";

        private enum SelectionMode
        {
            Single,
            Window,
            Cancel
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    DialogHelper.ShowError(Title, "Open a project view before running this command.");
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (doc.IsFamilyDocument)
                {
                    DialogHelper.ShowError(Title, "This tool runs in a project, not the Family Editor.");
                    return Result.Cancelled;
                }

                View view = doc.ActiveView;
                if (view == null || view.IsTemplate)
                {
                    DialogHelper.ShowError(Title, "Run this tool in a normal project view.");
                    return Result.Cancelled;
                }

                if (!IsSupportedViewType(view.ViewType))
                {
                    DialogHelper.ShowError(Title, "This tool only works in plan, section, or elevation views.");
                    return Result.Cancelled;
                }

                SelectionMode mode = PromptSelectionMode();
                if (mode == SelectionMode.Cancel)
                    return Result.Cancelled;

                var filter = new DatumSelectionFilter();
                int flippedCount = mode == SelectionMode.Single
                    ? HandleSinglePickLoop(uidoc, doc, view, filter)
                    : HandleWindowSelection(uidoc, doc, view, filter);

                // Normal success is silent - flipped bubbles are immediately visible in the view.
                return flippedCount == 0 ? Result.Cancelled : Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(Title, ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Picks datums one-by-one, flipping each instantly until the user presses Esc.
        /// Each flip is its own undo step (a transaction cannot remain open across a pick).
        /// </summary>
        private static int HandleSinglePickLoop(UIDocument uidoc, Document doc, View view, DatumSelectionFilter filter)
        {
            int flippedCount = 0;
            const string prompt = "Pick grids/levels to flip bubbles (Esc to finish)";

            while (true)
            {
                Reference pickedRef;
                try
                {
                    pickedRef = uidoc.Selection.PickObject(ObjectType.Element, filter, prompt);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                DatumPlane datum = doc.GetElement(pickedRef.ElementId) as DatumPlane;
                if (datum == null || !datum.IsValidObject || !datum.CanBeVisibleInView(view))
                    continue;

                if (TryFlipInTransaction(doc, datum, view))
                    flippedCount++;
            }

            return flippedCount;
        }

        /// <summary>
        /// Window-selects datums and flips each rectangle's worth of bubbles in a single undo step,
        /// repeating until the user presses Esc.
        /// </summary>
        private static int HandleWindowSelection(UIDocument uidoc, Document doc, View view, DatumSelectionFilter filter)
        {
            int flippedCount = 0;
            const string prompt = "Drag a window to select grids/levels (Esc to finish)";

            while (true)
            {
                IList<Element> picked;
                try
                {
                    picked = uidoc.Selection.PickElementsByRectangle(filter, prompt);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                var datums = new List<DatumPlane>();
                foreach (Element elem in picked)
                {
                    if (elem is DatumPlane datum && datum.IsValidObject && datum.CanBeVisibleInView(view))
                        datums.Add(datum);
                }

                if (datums.Count == 0)
                    continue;

                flippedCount += FlipBatchInTransaction(doc, datums, view);
            }

            return flippedCount;
        }

        private static bool IsSupportedViewType(ViewType viewType)
        {
            return viewType == ViewType.FloorPlan
                   || viewType == ViewType.CeilingPlan
                   || viewType == ViewType.EngineeringPlan
                   || viewType == ViewType.AreaPlan
                   || viewType == ViewType.Section
                   || viewType == ViewType.Elevation;
        }

        /// <summary>
        /// Moves the bubble to the opposite end of the datum in this view.
        /// </summary>
        private static bool Flip(DatumPlane datum, View view)
        {
            bool end0Visible = datum.IsBubbleVisibleInView(DatumEnds.End0, view);
            bool end1Visible = datum.IsBubbleVisibleInView(DatumEnds.End1, view);

            if (end0Visible && !end1Visible)
            {
                datum.HideBubbleInView(DatumEnds.End0, view);
                datum.ShowBubbleInView(DatumEnds.End1, view);
                return true;
            }

            if (end1Visible && !end0Visible)
            {
                datum.HideBubbleInView(DatumEnds.End1, view);
                datum.ShowBubbleInView(DatumEnds.End0, view);
                return true;
            }

            if (!end0Visible && !end1Visible)
            {
                datum.ShowBubbleInView(DatumEnds.End0, view);
                return true;
            }

            datum.HideBubbleInView(DatumEnds.End1, view);
            return true;
        }

        /// <summary>
        /// Flips a single datum inside its own transaction (one undo step).
        /// </summary>
        private static bool TryFlipInTransaction(Document doc, DatumPlane datum, View view)
        {
            using (Transaction t = new Transaction(doc, TransactionName))
            {
                t.Start();
                try
                {
                    if (Flip(datum, view))
                    {
                        t.Commit();
                        return true;
                    }
                }
                catch
                {
                    // Skip a datum this view will not let us change (e.g. owned by another user).
                }

                t.RollBack();
                return false;
            }
        }

        /// <summary>
        /// Flips a batch of datums inside one transaction so the whole window-select is a single undo
        /// step. Datums that cannot be changed are skipped. Returns how many were flipped.
        /// </summary>
        private static int FlipBatchInTransaction(Document doc, IList<DatumPlane> datums, View view)
        {
            int flipped = 0;

            using (Transaction t = new Transaction(doc, TransactionName))
            {
                t.Start();
                foreach (DatumPlane datum in datums)
                {
                    try
                    {
                        if (Flip(datum, view))
                            flipped++;
                    }
                    catch
                    {
                        // Skip a datum this view will not let us change; keep flipping the rest.
                    }
                }

                if (flipped > 0)
                    t.Commit();
                else
                    t.RollBack();
            }

            return flipped;
        }

        private static SelectionMode PromptSelectionMode()
        {
            TaskDialog dialog = new TaskDialog(Title)
            {
                MainInstruction = "Select grids or levels to flip bubbles",
                MainContent = "Choose how you want to select. You can keep picking or windowing until you press Esc.",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Pick individually (instant flip)");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Window select multiple");

            TaskDialogResult result = dialog.Show();

            if (result == TaskDialogResult.CommandLink1)
                return SelectionMode.Single;
            if (result == TaskDialogResult.CommandLink2)
                return SelectionMode.Window;
            return SelectionMode.Cancel;
        }
    }
}
