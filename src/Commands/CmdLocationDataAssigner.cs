#region Metadata
/*
 * Tool Name     : Assign Location (Location Data Assigner)
 * File Name     : CmdLocationDataAssigner.cs
 * Purpose       : Opens the Location Data Assigner window where the user writes Room, Level, Coordinates,
 *                 Altitude, and HVAC Zone data onto selected categories.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-09
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI (LocationDataAssignerWindow), AJTools.Utils
 *
 * Input         : Active project - categories and data options chosen in the window.
 * Output        : Location parameters written to matching elements (transaction owned by the window).
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Project-only tool; validates an editable, non-family document before opening the window.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-04-09) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Assignment behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;
using AJTools.Utils;
using System.Windows.Interop;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens the Location Data Assigner dialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdLocationDataAssigner : IExternalCommand
    {
        private const string Title = "Location Data Assigner";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                if (!ValidationHelper.ValidateEditableDocument(uidoc.Document, out message))
                {
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                var window = new LocationDataAssignerWindow(uidoc.Document);
                var helper = new WindowInteropHelper(window)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
