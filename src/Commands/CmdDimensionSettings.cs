#region Metadata
/*
 * Tool Name     : Dimension Settings (Automatic Dimension / Auto MEP Dimension)
 * File Name     : CmdDimensionSettings.cs
 * Purpose       : Ribbon entry commands that open the two dimension settings windows and save the result.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Dimensioning, AJTools.Services.Dimensioning,
 *                 AJTools.UI.Dimensioning, AJTools.Utils
 *
 * Input         : The saved settings, plus the linear dimension type names in the open project.
 * Output        : Updated settings written to the user's AppData folder. The model is not touched.
 *
 * Notes         :
 * - Settings-only tools: they read the project's dimension types but modify nothing, so no transaction
 *   is opened. The [Transaction(TransactionMode.Manual)] attribute is still required by Revit.
 * - Modal WPF windows shown from inside Execute, owned by the Revit main window so they cannot drop
 *   behind it.
 * - The windows are pure UI; these commands read the type list, own the save, and report a save failure.
 * - Cancel closes silently, per the house no-success-popup rule.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.Dimensioning;
using AJTools.Services.Dimensioning;
using AJTools.UI.Dimensioning;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>Opens the Automatic Dimension (grids / levels) settings.</summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdAutoDimensionSettings : IExternalCommand
    {
        private const string ToolTitle = "Automatic Dimension Settings";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData?.Application;
                UIDocument uidoc = uiapp?.ActiveUIDocument;

                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                IList<string> typeNames = DimensionFactory.GetLinearDimensionTypeNames(doc);

                AutoDatumDimensionSettingsWindow window =
                    new AutoDatumDimensionSettingsWindow(AutoDatumDimensionSettingsService.Load(), typeNames);

                if (uiapp != null)
                {
                    new WindowInteropHelper(window)
                    {
                        Owner = uiapp.MainWindowHandle
                    };
                }

                if (window.ShowDialog() != true)
                    return Result.Cancelled;

                if (!AutoDatumDimensionSettingsService.Save(window.ResultSettings, out string saveError))
                {
                    DialogHelper.ShowError(ToolTitle, "The settings could not be saved:\n" + saveError);
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>Opens the Auto MEP Dimension settings.</summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMepDimensionSettings : IExternalCommand
    {
        private const string ToolTitle = "Auto MEP Dimension Settings";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData?.Application;
                UIDocument uidoc = uiapp?.ActiveUIDocument;

                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                IList<string> typeNames = DimensionFactory.GetLinearDimensionTypeNames(doc);

                MepDimensionSettingsWindow window =
                    new MepDimensionSettingsWindow(MepDimensionSettingsService.Load(), typeNames);

                if (uiapp != null)
                {
                    new WindowInteropHelper(window)
                    {
                        Owner = uiapp.MainWindowHandle
                    };
                }

                if (window.ShowDialog() != true)
                    return Result.Cancelled;

                if (!MepDimensionSettingsService.Save(window.ResultSettings, out string saveError))
                {
                    DialogHelper.ShowError(ToolTitle, "The settings could not be saved:\n" + saveError);
                    return Result.Failed;
                }

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
