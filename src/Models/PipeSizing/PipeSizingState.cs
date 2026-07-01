#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeSizingState.cs
 * Purpose       : Stores last-used Pipe Sizing window settings and fixture rows.
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
 * Dependencies  : Newtonsoft.Json through PipeSizingStateService
 *
 * Input         : System type, material, velocity limit, and fixture rows.
 * Output        : Serializable state matching the Python tool's state shape.
 *
 * Notes         :
 * - JSON property names match the original pyRevit pipe sizing state file.
 * - The C# add-in stores the file in the user's AJ Tools AppData folder.
 *
 * Changelog     :
 * v1.0.0 (2026-07-01) - Initial C# port for Pipe Sizing.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;

namespace AJTools.Models.PipeSizing
{
    internal sealed class PipeSizingState
    {
        public int SystemType { get; set; }
        public int PipeMaterial { get; set; }
        public string VelocityMS { get; set; }
        public string VelocityFTS { get; set; }
        public List<PipeSizingStateRow> Rows { get; set; }

        public PipeSizingState()
        {
            VelocityMS = "2.4";
            VelocityFTS = "7.87";
            Rows = new List<PipeSizingStateRow>();
        }
    }

    internal sealed class PipeSizingStateRow
    {
        public string Fixture { get; set; }
        public bool IsCold { get; set; }
        public bool IsHot { get; set; }
        public string Qty { get; set; }

        public PipeSizingStateRow()
        {
            Fixture = string.Empty;
            Qty = "1";
        }
    }
}
