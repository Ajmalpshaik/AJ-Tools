#region Metadata
/*
 * Tool Name     : Pin / Unpin Elements
 * File Name     : CmdPinElements.cs
 * Purpose       : Opens the Pin Elements window where the user picks Sheet groups (title blocks, placed
 *                 views, legends, schedules) or Model groups (duct, pipe, cable tray, generic models,
 *                 mechanical equipment, plumbing fixtures, electrical equipment, grids, levels) and pins
 *                 or unpins them.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.3.0
 *
 * Created Date  : 2026-04-10
 * Last Updated  : 2026-07-13
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI (PinElementsWindow), AJTools.Services.PinTools, AJTools.Utils
 *
 * Input         : Active project - target groups and Active-Sheet-Only / All-Sheets mode chosen in the window.
 * Output        : Selected groups pinned or unpinned; the window reports counts. Transaction owned by the service.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Project-only tool; validates an editable, non-family document before opening the window.
 * - The window is modal and owned by the Revit main window so it stays on top.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.1.0 (2026-04-18) - Sheet/Model group selection with active-sheet and all-sheets modes.
 * v1.2.0 (2026-07-01) - Refactor/audit: added full metadata block. Pin behaviour unchanged.
 * v1.3.0 (2026-07-13) - Added Grids and Levels as additional Model groups (datum elements, same
 *                       category-collection pattern as the existing groups).
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens a fixed-group selector and applies pin or unpin.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdPinElements : IExternalCommand
    {
        private const string Title = "Pin Elements";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                {
                    DialogHelper.ShowError(Title, message);
                    return Result.Cancelled;
                }

                var window = new PinElementsWindow(doc);
                new WindowInteropHelper(window)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

                bool? dialogResult = window.ShowDialog();
                if (dialogResult != true && !window.HasExecutedOperation)
                    return Result.Cancelled;
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
