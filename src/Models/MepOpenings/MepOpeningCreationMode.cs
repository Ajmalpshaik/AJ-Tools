#region Metadata
/*
 * Tool Name     : Opening
 * File Name     : MepOpeningCreationMode.cs
 * Purpose       : Defines how opening requests should be created.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-04
 * Last Updated  : 2026-07-04
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : Opening Settings selection.
 * Output        : Direct opening or family opening intent.
 *
 * Notes         :
 * - Direct openings can only modify current-model hosts.
 * - Linked-model hosts require family opening workflow.
 *
 * Changelog     :
 * v1.0.0 (2026-07-04) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.MepOpenings
{
    public enum MepOpeningCreationMode
    {
        DirectOpening = 0,
        FamilyOpening = 1
    }
}
