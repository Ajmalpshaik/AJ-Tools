#region Metadata
/*
 * Tool Name     : Fix Tag Clash Settings
 * File Name     : CmdFixTagClashSettings.cs
 * Purpose       : Opens the settings window for the tag clash engine and saves the result.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-16
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.UI.TagClash (FixTagClashSettingsWindow),
 *                 AJTools.Utils (TagClashSettings, DialogHelper)
 *
 * Input         : The stored settings.
 * Output        : The settings file, rewritten - and verified by reading it straight back.
 *
 * Notes         :
 * - No open project is needed: these settings belong to the add-in, not to a model.
 * - The save is verified by read-back rather than assumed, the same lesson Arrange Tags Settings
 *   learned - a settings write that silently fails is worse than one that reports the problem.
 *
 * Changelog     :
 * v1.0.0 (2026-08-16) - Initial release.
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
using AJTools.UI.TagClash;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens the Fix Tag Clash settings window.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdFixTagClashSettings : IExternalCommand
    {
        private const string ToolTitle = "Fix Tag Clash Settings";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData?.Application;
                if (uiapp == null)
                {
                    message = "No Revit session available.";
                    return Result.Failed;
                }

                TagClashSettingsState current = TagClashSettings.Load();

                var window = new FixTagClashSettingsWindow(current);
                new WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };

                if (window.ShowDialog() != true || window.Result == null)
                    return Result.Cancelled;

                if (!TagClashSettings.Save(window.Result))
                {
                    DialogHelper.ShowError(
                        ToolTitle,
                        "The settings could not be saved. Check that AJ Tools can write to your AppData folder, then try again.");
                    return Result.Failed;
                }

                // Settings writes are easy to lose silently - confirm the values actually stuck.
                TagClashSettingsState stored = TagClashSettings.Load();
                if (stored.FixPasses != TagClashSettings.ClampPasses(window.Result.FixPasses)
                    || Math.Abs(stored.MaxDriftMm - TagClashSettings.ClampDrift(window.Result.MaxDriftMm)) > 0.0005
                    || stored.MarkColour != window.Result.MarkColour
                    || stored.MarkFailures != window.Result.MarkFailures
                    || stored.SkipVerticalRuns != window.Result.SkipVerticalRuns
                    || stored.FullSearch != window.Result.FullSearch)
                {
                    DialogHelper.ShowError(
                        ToolTitle,
                        "The settings were written but did not read back correctly. Please try again.");
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
