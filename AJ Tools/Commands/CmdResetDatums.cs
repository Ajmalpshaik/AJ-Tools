using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    internal enum ResetDatumMode
    {
        Combined,
        GridsOnly,
        LevelsOnly
    }

    internal static class ResetDatumService
    {
        internal static Result Execute(
            ExternalCommandData commandData,
            ResetDatumMode mode,
            string title)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null)
                {
                    TaskDialog.Show(title, "Open a project view before running this command.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                View view = doc.ActiveView;

                if (view == null || view.IsTemplate)
                {
                    TaskDialog.Show(title, "Please run this tool inside a normal project view.");
                    return Result.Failed;
                }

                int gridCount = 0;
                int levelCount = 0;

                using (Transaction t = new Transaction(doc, "AJ Tools - Reset Datums"))
                {
                    t.Start();

                    if (mode != ResetDatumMode.LevelsOnly)
                        gridCount = ResetGrids(doc, view);

                    if (mode != ResetDatumMode.GridsOnly)
                        levelCount = ResetLevels(doc, view);

                    t.Commit();
                }

                if (gridCount == 0 && levelCount == 0)
                {
                    TaskDialog.Show(
                        title,
                        "No visible grids or levels were found to reset in this view.");
                    return Result.Cancelled;
                }

                List<string> parts = new List<string>();
                if (gridCount > 0)
                    parts.Add(string.Format("{0} grid(s)", gridCount));
                if (levelCount > 0)
                    parts.Add(string.Format("{0} level(s)", levelCount));

                TaskDialog.Show(
                    title,
                    string.Format("Successfully reset {0} to 3D extents in this view.", string.Join(" and ", parts)));
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(title, ex.Message);
                return Result.Failed;
            }
        }

        private static int ResetGrids(Document doc, View view)
        {
            IList<Element> grids = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .ToElements();

            int resetCount = 0;
            foreach (Element element in grids)
            {
                Grid grid = element as Grid;
                if (grid == null)
                    continue;

                bool resetPerformed = false;
                foreach (DatumEnds end in new[] { DatumEnds.End0, DatumEnds.End1 })
                {
                    try
                    {
                        grid.SetDatumExtentType(end, view, DatumExtentType.Model);
                        resetPerformed = true;
                    }
                    catch
                    {
                        // Skip ends not available in this view (e.g., when not cropped).
                    }
                }

                if (resetPerformed)
                    resetCount++;
            }

            return resetCount;
        }

        private static int ResetLevels(Document doc, View view)
        {
            IList<Element> levels = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Level))
                .ToElements();

            int resetCount = 0;
            List<DatumEnds> datumEnds = new List<DatumEnds>
            {
                DatumEnds.End0,
                DatumEnds.End1
            };

            if (Enum.IsDefined(typeof(DatumEnds), "End2"))
                datumEnds.Add((DatumEnds)Enum.Parse(typeof(DatumEnds), "End2"));
            if (Enum.IsDefined(typeof(DatumEnds), "End3"))
                datumEnds.Add((DatumEnds)Enum.Parse(typeof(DatumEnds), "End3"));

            foreach (Element element in levels)
            {
                Level level = element as Level;
                if (level == null)
                    continue;

                bool resetPerformed = false;
                foreach (DatumEnds end in datumEnds)
                {
                    try
                    {
                        level.SetDatumExtentType(end, view, DatumExtentType.Model);
                        resetPerformed = true;
                    }
                    catch
                    {
                        // Some ends don't apply in every context, so skip failures.
                    }
                }

                if (resetPerformed)
                    resetCount++;
            }

            return resetCount;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdResetDatums : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ResetDatumService.Execute(
                commandData,
                ResetDatumMode.Combined,
                "Reset Grids & Levels");
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdResetDatumsGrids : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ResetDatumService.Execute(
                commandData,
                ResetDatumMode.GridsOnly,
                "Reset Grids");
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdResetDatumsLevels : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ResetDatumService.Execute(
                commandData,
                ResetDatumMode.LevelsOnly,
                "Reset Levels");
        }
    }
}
