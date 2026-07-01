#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeSizeOption.cs
 * Purpose       : Stores one selectable pipe size with internal diameter and display label.
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
 * Input         : Pipe internal diameter in millimeters and nominal display text.
 * Output        : Immutable pipe-size lookup item.
 *
 * Notes         :
 * - Ported from the original pyRevit pipe sizing constants.
 * - Pipe ID values are intentionally kept unchanged from the Python tool.
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
    internal sealed class PipeSizeOption
    {
        public PipeSizeOption(double internalDiameterMm, string nominalLabel)
        {
            InternalDiameterMm = internalDiameterMm;
            NominalLabel = nominalLabel;
        }

        public double InternalDiameterMm { get; }
        public string NominalLabel { get; }
    }
}
