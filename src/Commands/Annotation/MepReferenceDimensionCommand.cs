#region Metadata
/*
 * Tool Name     : Auto MEP Dimension
 * File Name     : MepReferenceDimensionCommand.cs
 * Purpose       : Ribbon entry commands for Auto MEP Dimension - pick runs one at a time, dimension the
 *                 current selection, or dimension every eligible run in the active view. Each loads the
 *                 saved settings, calls MepReferenceDimensionService, and shows the run report.
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
 * Dependencies  : Autodesk Revit API, AJTools.Services.MepReferenceDimension, AJTools.Services.Dimensioning,
 *                 AJTools.Utils
 *
 * Input         : Active plan/section view; picked runs, the current selection, or the whole view.
 * Output        : Dimensions in the model, plus the run report shown here.
 *
 * Notes         :
 * - The service does the model work and RETURNS its report; this command decides what to show. Shared
 *   code that raises its own dialog blocks Revit when driven from anywhere but the ribbon.
 * - The report is shown whenever the settings ask for it, or whenever nothing was created. The tool it
 *   replaces built the same report and never displayed it, so a batch run finished in silence.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release, replacing DuctReferenceDimensionCommand.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.Dimensioning;
using AJTools.Services.Dimensioning;
using AJTools.Services.MepReferenceDimension;
using AJTools.Utils;

namespace AJTools.Commands.Annotation
{
    /// <summary>Shared execution for the three Auto MEP Dimension entry points.</summary>
    internal static class MepReferenceDimensionCommandRunner
    {
        internal static Result Run(ExternalCommandData commandData, MepDimensionMode mode, string title)
        {
            MepDimensionSettings settings = MepDimensionSettingsService.Load();

            MepDimensionRunResult result = MepReferenceDimensionService.Execute(
                commandData, mode, settings, title, out string blockingMessage);

            if (!string.IsNullOrWhiteSpace(blockingMessage))
            {
                DialogHelper.ShowError(title, blockingMessage);
                return result.Status == Result.Failed ? Result.Failed : Result.Cancelled;
            }

            DimensionRunReport report = result.Report;

            // Nothing placed: always say why, since there is nothing on screen to look at.
            if (report.DimensionsCreated == 0)
            {
                DialogHelper.ShowInfo(
                    title,
                    report.HasActivity
                        ? report.BuildSummary()
                        : "Nothing here matched the settings, so no dimensions were created.");

                return Result.Cancelled;
            }

            // It worked and nothing was left out - stay quiet. The dimensions are the result; a popup
            // confirming what is already visible on screen is just another click.
            if (settings.ShowReport && report.HasUnfinishedWork)
                DialogHelper.ShowInfo(title, report.BuildSummary());

            return Result.Succeeded;
        }
    }

    /// <summary>Pick runs one at a time until ESC.</summary>
    [Transaction(TransactionMode.Manual)]
    public class MepReferenceDimensionCommand : IExternalCommand
    {
        private const string ToolTitle = "Auto MEP Dimension";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return MepReferenceDimensionCommandRunner.Run(commandData, MepDimensionMode.PickRuns, ToolTitle);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>Dimension whatever is selected when the command starts.</summary>
    [Transaction(TransactionMode.Manual)]
    public class MepReferenceDimensionSelectionCommand : IExternalCommand
    {
        private const string ToolTitle = "Auto MEP Dimension - Selection";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return MepReferenceDimensionCommandRunner.Run(commandData, MepDimensionMode.CurrentSelection, ToolTitle);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>Dimension every eligible run in the active view.</summary>
    [Transaction(TransactionMode.Manual)]
    public class MepReferenceDimensionActiveViewCommand : IExternalCommand
    {
        private const string ToolTitle = "Auto MEP Dimension - Active View";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return MepReferenceDimensionCommandRunner.Run(commandData, MepDimensionMode.ActiveView, ToolTitle);
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
