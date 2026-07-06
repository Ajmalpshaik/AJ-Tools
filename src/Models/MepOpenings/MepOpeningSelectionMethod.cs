#region Metadata
/*
 * Tool Name     : Opening
 * File Name     : MepOpeningSelectionMethod.cs
 * Purpose       : Defines whether the create tool starts from selected MEP sources or selected opening hosts.
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
 * Output        : Source-element or host-element selection workflow.
 *
 * Notes         :
 * - Source Elements is for selecting ducts, pipes, cable trays, or conduits.
 * - Host Elements is for selecting walls, floors/slabs, or beams.
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
    public enum MepOpeningSelectionMethod
    {
        SourceElements = 0,
        HostElements = 1
    }
}
