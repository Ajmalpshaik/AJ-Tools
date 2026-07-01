#region Metadata
/*
 * Tool Name     : Link Workset (Set Link Workset)
 * File Name     : CmdSetLinkWorkset.cs
 * Purpose       : Opens the Set Link Workset window where the user assigns selected Revit links and CAD
 *                 imports to an existing or new workset.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-23
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI (SetLinkWorksetWindow), AJTools.Utils
 *
 * Input         : Active project - links/CAD imports and the target workset chosen in the window.
 * Output        : Selected links/imports assigned to the chosen workset (transaction owned by the window).
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Validates an active document before opening the window; the window is owned by the Revit main window.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-23) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Workset-assignment behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using AJTools.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens the Set Link Workset dialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSetLinkWorkset : IExternalCommand
    {
        private const string Title = "Set Link Workset";

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

                var window = new SetLinkWorksetWindow(uidoc.Document);
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
