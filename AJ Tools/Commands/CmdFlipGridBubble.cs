using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Exceptions;

namespace AJTools
{
    internal class DatumSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Grid || elem is Level;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
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
                TaskDialog.Show("Flip Datum Bubble", "Open a project view before running this command.");
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (view == null || view.IsTemplate)
            {
                TaskDialog.Show("Flip Datum Bubble", "Please run this tool in a normal project view.");
                return Result.Failed;
            }

            int flipped = 0;
            DatumSelectionFilter filter = new DatumSelectionFilter();

            SelectionMode mode = PromptSelectionMode();
            if (mode == SelectionMode.Cancel)
                return Result.Cancelled;

            if (mode == SelectionMode.Single)
            {
                flipped += HandleSinglePicks(uidoc, doc, view, filter);
            }
            else if (mode == SelectionMode.Window)
            {
                flipped += HandleWindowSelection(uidoc, doc, view, filter);
            }

            if (flipped == 0)
                return Result.Cancelled;

            TaskDialog.Show("Flip Datum Bubble", string.Format("{0} datum(s) flipped.", flipped));
            return Result.Succeeded;
        }

        private static int HandleSinglePicks(UIDocument uidoc, Document doc, View view, DatumSelectionFilter filter)
        {
            int flipped = 0;
            while (true)
            {
                Reference picked = null;
                try
                {
                    picked = uidoc.Selection.PickObject(ObjectType.Element, filter, "Pick grids/levels to flip bubbles (Esc to stop picking)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                if (picked == null)
                    break;

                DatumPlane datum = doc.GetElement(picked.ElementId) as DatumPlane;
                if (datum == null)
                    continue;

                if (!datum.CanBeVisibleInView(view))
                    continue;

                if (FlipWithTransaction(doc, datum, view))
                    flipped++;
            }

            return flipped;
        }

        private static int HandleWindowSelection(UIDocument uidoc, Document doc, View view, DatumSelectionFilter filter)
        {
            int flipped = 0;
            while (true)
            {
                IList<Element> picked = null;
                try
                {
                    picked = uidoc.Selection.PickElementsByRectangle(filter, "Drag a window to select grids/levels (Esc to stop)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                foreach (Element elem in picked)
                {
                    DatumPlane datum = elem as DatumPlane;
                    if (datum == null)
                        continue;
                    if (!datum.CanBeVisibleInView(view))
                        continue;

                    if (FlipWithTransaction(doc, datum, view))
                        flipped++;
                }
            }

            return flipped;
        }

        private static bool Flip(DatumPlane datum, View view)
        {
            IList<DatumEnds> ends = new List<DatumEnds> { DatumEnds.End0, DatumEnds.End1 };
            bool firstHidden = false;
            bool changed = false;

            foreach (DatumEnds end in ends)
            {
                if (datum.IsBubbleVisibleInView(end, view) && !firstHidden)
                {
                    datum.HideBubbleInView(end, view);
                    firstHidden = true;
                    changed = true;
                }
                else
                {
                    if (!datum.IsBubbleVisibleInView(end, view))
                    {
                        datum.ShowBubbleInView(end, view);
                        changed = true;
                    }
                }
            }

            return changed;
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
                CommonButtons = TaskDialogCommonButtons.Cancel,
                AllowCancellation = true
            };

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Pick individually");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Window select multiple");

            TaskDialogResult result = dialog.Show();
            if (result == TaskDialogResult.CommandLink1) return SelectionMode.Single;
            if (result == TaskDialogResult.CommandLink2) return SelectionMode.Window;
            return SelectionMode.Cancel;
        }

        private enum SelectionMode
        {
            Single,
            Window,
            Cancel
        }
    }
}
