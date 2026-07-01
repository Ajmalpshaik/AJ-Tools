#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeFixtureData.cs
 * Purpose       : Stores fixture-unit values for one plumbing fixture row.
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
 * Input         : Fixture cold, hot, and combined fixture-unit values.
 * Output        : Immutable fixture-unit lookup item.
 *
 * Notes         :
 * - Ported from the original pyRevit pipe sizing constants.
 * - Values are intentionally kept unchanged from the Python tool.
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
    internal sealed class PipeFixtureData
    {
        public PipeFixtureData(double cold, double hot, double total)
        {
            Cold = cold;
            Hot = hot;
            Total = total;
        }

        public double Cold { get; }
        public double Hot { get; }
        public double Total { get; }
    }
}
