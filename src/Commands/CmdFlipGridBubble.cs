// Tool Name: Flip Grid Bubble
// Description: Instantly flips the bubble of a picked grid/level in plan/section/elevation views.
// Author: Ajmal P.S.
// Version: 1.1.1
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Utils;

namespace AJTools.Commands
{
    internal enum SelectionMode
    {
        Single,
        Window,
        Cancel
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdFlipGridBubble : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (view == null || view.IsTemplate)
            {
                message = "Run this tool in a normal project view.";
                return Result.Failed;
            }

            if (!IsSupportedViewType(view.ViewType))
            {
                message = "This tool only works in plan, section, or elevation views.";
                return Result.Failed;
            }

            DatumSelectionFilter filter = new DatumSelectionFilter();
            SelectionMode mode = PromptSelectionMode();
            if (mode == SelectionMode.Cancel)
                return Result.Cancelled;

            int flippedCount = mode == SelectionMode.Single
                ? HandleSinglePickLoop(uidoc, doc, view, filter)
                : HandleWindowSelection(uidoc, doc, view, filter);

            // ❌ Removed final popup completely.
            return flippedCount == 0 ? Result.Cancelled : Result.Succeeded;
        }

        private static int HandleSinglePickLoop(
            UIDocument uidoc,
            Document doc,
            View view,
            DatumSelectionFilter filter)
        {
            int flippedCount = 0;
            const string prompt = "Pick grids/levels to flip bubbles (Esc to finish)";

            while (true)
            {
                Reference pickedRef;
                try
                {
                    pickedRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        filter,
                        prompt);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                Element elem = doc.GetElement(pickedRef.ElementId);
                DatumPlane datum = elem as DatumPlane;
                if (datum == null || !datum.CanBeVisibleInView(view))
                    continue;

                if (FlipWithTransaction(doc, datum, view))
                    flippedCount++;
            }

            return flippedCount;
        }

        private static int HandleWindowSelection(
            UIDocument uidoc,
            Document doc,
            View view,
            DatumSelectionFilter filter)
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

                foreach (Element elem in picked)
                {
                    DatumPlane datum = elem as DatumPlane;
                    if (datum == null || !datum.CanBeVisibleInView(view))
                        continue;

                    if (FlipWithTransaction(doc, datum, view))
                        flippedCount++;
                }
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

        private static bool FlipWithTransaction(Document doc, DatumPlane datum, View view)
        {
            using (Transaction t = new Transaction(doc, "Flip Datum Bubble"))
            {
                t.Start();
                bool changed = Flip(datum, view);
                if (changed)
                {
                    t.Commit();
                    return true;
                }
                t.RollBack();
                return false;
            }
        }

        private static SelectionMode PromptSelectionMode()
        {
            TaskDialog dialog = new TaskDialog("Flip Datum Bubble")
            {
                MainInstruction = "Select grids or levels to flip bubbles",
                MainContent = "Choose how you want to select. You can keep picking or windowing until you press Esc.",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink1,
                "Pick individually (instant flip)");

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink2,
                "Window select multiple");

            TaskDialogResult result = dialog.Show();

            if (result == TaskDialogResult.CommandLink1)
                return SelectionMode.Single;
            if (result == TaskDialogResult.CommandLink2)
                return SelectionMode.Window;
            return SelectionMode.Cancel;
        }
    }
}
