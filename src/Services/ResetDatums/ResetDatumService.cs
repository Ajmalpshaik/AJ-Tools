#region Metadata
/*
 * Tool Name     : Reset Grid / Level Extents to 3D
 * File Name     : ResetDatumService.cs
 * Purpose       : Switches every visible grid/level datum end in the active view back to 3D (Model)
 *                 extent type, so the datum follows its shared 3D extent again.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View - all visible grids and/or levels (per ResetDatumMode).
 * Output        : Grid/level datum ends switched to 3D (Model) extents; single undo step.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. SetDatumExtentType is stable across all target versions.
 * - Project-only tool; exits cleanly in the Family Editor.
 * - Normal success is silent (the view updates visibly); only the empty case and errors are reported.
 * - Production-ready implementation with safe single-transaction handling.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.1.0 (2026-06-30) - Added mandatory metadata block; removed success popup (silent success);
 *                       routed messages through DialogHelper; added Family-Editor and validity
 *                       guards. Reset behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models;
using AJTools.Utils;

namespace AJTools.Services.ResetDatums
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
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    DialogHelper.ShowError(title, "Open a project view before running this command.");
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (doc.IsFamilyDocument)
                {
                    DialogHelper.ShowError(title, "This tool runs in a project, not the Family Editor.");
                    return Result.Cancelled;
                }

                View view = doc.ActiveView;
                if (view == null || view.IsTemplate)
                {
                    DialogHelper.ShowError(title, "Please run this tool inside a normal project view.");
                    return Result.Cancelled;
                }

                int gridCount = 0;
                int levelCount = 0;

                using (Transaction t = new Transaction(doc, "AJ Tools - Reset Grid / Level Extents to 3D"))
                {
                    t.Start();

                    if (mode != ResetDatumMode.LevelsOnly)
                        gridCount = ResetDatums(doc, view, typeof(Grid));

                    if (mode != ResetDatumMode.GridsOnly)
                        levelCount = ResetDatums(doc, view, typeof(Level));

                    t.Commit();
                }

                if (gridCount == 0 && levelCount == 0)
                {
                    DialogHelper.ShowInfo(
                        title,
                        "No grids or levels were found to reset in this view. " +
                        "Open a view that shows grids or levels and try again.");
                    return Result.Cancelled;
                }

                // Normal success is silent - the datums update visibly in the view.
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(title, ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Resets every visible datum of the given type (Grid or Level) in the view to 3D (Model)
        /// extents and returns how many were reset.
        /// </summary>
        private static int ResetDatums(Document doc, View view, Type datumClass)
        {
            int resetCount = 0;

            foreach (Element element in new FilteredElementCollector(doc, view.Id).OfClass(datumClass))
            {
                DatumPlane datum = element as DatumPlane;
                if (datum == null || !datum.IsValidObject)
                    continue;

                bool resetPerformed = false;

                foreach (DatumEnds end in new[] { DatumEnds.End0, DatumEnds.End1 })
                {
                    try
                    {
                        datum.SetDatumExtentType(end, view, DatumExtentType.Model);
                        resetPerformed = true;
                    }
                    catch
                    {
                        // Skip a datum end that this view does not allow to be reset
                        // (e.g. owned by another user, or end not available in this view).
                    }
                }

                if (resetPerformed)
                    resetCount++;
            }

            return resetCount;
        }
    }
}
