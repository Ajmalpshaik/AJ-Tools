#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningHostKind.cs
 * Purpose       : Lists supported host element groups for direct opening creation.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : Host element category (current model, or a linked model in Family Opening mode).
 * Output        : Host group for opening creation and reports.
 *
 * Notes         :
 * - Direct Opening mode cuts current-model walls, floors/slabs, and structural framing beams.
 * - Linked-model hosts are never modified: Family Opening mode handles linked walls by placing an
 *   opening family in the current model (Source Elements selection).
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 * v1.0.1 (2026-10-12) - Notes only: linked hosts are no longer "future" - Family Opening mode handles
 *                       linked walls.
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
