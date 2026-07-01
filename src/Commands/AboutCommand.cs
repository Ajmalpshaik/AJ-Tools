#region Metadata
/*
 * Tool Name     : About
 * File Name     : AboutCommand.cs
 * Purpose       : Opens the AJ Tools About window showing version, platform details, developer info,
 *                 update notes, and repository links.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI (AboutWindow)
 *
 * Input         : None - opens an information window.
 * Output        : About window (read-only; no model change).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Read-only tool; makes no model changes.
 * - Window is owned by the Revit main window.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-01) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. About behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;
using System.Windows.Interop;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AboutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new AboutWindow(commandData.Application);
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
