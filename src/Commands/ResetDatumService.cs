// Tool Name: Reset Datums Service
// Description: Core service to reset grid and level datum extents back to model (3D) extents.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Resets grid and level datum extents in the active view back to model (3D) extents.
    /// </summary>
    internal static class ResetDatumService
    {
        /// <summary>
        /// Runs the reset workflow for grids/levels according to the selected mode.
        /// </summary>
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

        /// <summary>
        /// Resets visible grids to 3D extents and returns the count reset.
        /// </summary>
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
                        // Ignore if datum end is not available in this view (e.g., when not cropped).
                    }
                }

                if (resetPerformed)
                    resetCount++;
            }

            return resetCount;
        }

        /// <summary>
        /// Resets visible levels to 3D extents and returns the count reset.
        /// </summary>
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
                        // Ignore if datum end is not available in every context, so skip failures.
                    }
                }

                if (resetPerformed)
                    resetCount++;
            }

            return resetCount;
        }
    }
}
