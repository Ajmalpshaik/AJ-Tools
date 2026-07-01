#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeSizingResult.cs
 * Purpose       : Holds the calculated sizing outputs shown in the Pipe Sizing window.
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
 * Dependencies  : None
 *
 * Input         : Calculated flow, pipe ID, selected size, velocity, and friction loss.
 * Output        : Pipe sizing result data for UI and CSV export.
 *
 * Notes         :
 * - Mirrors the values returned by the original pyRevit pipe sizing calculator.
 *
 * Changelog     :
 * v1.0.0 (2026-07-01) - Initial C# port for Pipe Sizing.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.PipeSizing
{
    internal sealed class PipeSizingResult
    {
        public double FlowGpm { get; set; }
        public double SelectedSizeMm { get; set; }
        public string SelectedSizeLabel { get; set; }
        public double RequiredInternalDiameterMm { get; set; }
        public double VelocityFeetPerSecond { get; set; }
        public double FrictionLossPsiPer100Ft { get; set; }
    }
}
