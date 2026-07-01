#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : CmdPipeSizing.cs
 * Purpose       : Revit external command entry point for the Pipe Sizing calculator.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, PipeSizingWindow
 *
 * Input         : User-entered fixture rows in the Pipe Sizing window.
 * Output        : Opens the Pipe Sizing calculator; no Revit model changes.
 *
 * Notes         :
 * - Ported from the original pyRevit pipe sizing entry point.
 * - No Transaction is started because this tool does not write to the Revit model.
 * - No PickObject/PickPoint calls are used.
 *
 * Changelog     :
 * v1.0.0 (2026-07-01) - Initial C# port for Pipe Sizing.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI.PipeSizing;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdPipeSizing : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new PipeSizingWindow();
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Pipe Sizing", "Pipe Sizing could not start:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
