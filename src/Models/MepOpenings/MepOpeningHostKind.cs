#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningHostKind.cs
 * Purpose       : Lists supported host element groups for direct opening creation.
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
 * Dependencies  : None
 *
 * Input         : Current-model host element category.
 * Output        : Host group for opening creation and reports.
 *
 * Notes         :
 * - v1.0.0 supports current-model walls, floors/slabs, and structural framing beams.
 * - Linked-model hosts are intentionally left for a future version.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.MepOpenings
{
    public enum MepOpeningHostKind
    {
        Wall = 0,
        FloorSlab = 1,
        Beam = 2
    }
}
