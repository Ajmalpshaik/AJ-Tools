#region Metadata
/*
 * Tool Name     : Opening Settings
 * File Name     : CmdMepOpeningSettings.cs
 * Purpose       : Revit external command entry point for opening settings.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-03
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, MepOpeningSettingsWindow
 *
 * Input         : Active project and user-edited settings in the Opening Settings window.
 * Output        : Saved settings; no Revit model changes.
 *
 * Notes         :
 * - Settings-only command. No transaction is started.
 * - Window is owned by the Revit main window.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI.MepOpenings;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdMepOpeningSettings : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application?.ActiveUIDocument?.Document;
                var window = new MepOpeningSettingsWindow(doc);
                var helper = new WindowInteropHelper(window)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

                bool? dialogResult = window.ShowDialog();
                return dialogResult == true ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Opening Settings", "Opening settings could not start:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
