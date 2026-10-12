#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningElementKind.cs
 * Purpose       : Lists the supported MEP element types for automatic opening creation.
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
 * Input         : Selection-scope MEP category classification.
 * Output        : Supported element kind used by settings, selection filters, and reports.
 *
 * Notes         :
 * - Supports pipes, ducts, cable trays, and conduits only; cable tray runs and cable tray fittings
 *   count as Cable Tray.
 * - Pipe, duct, and conduit fittings, accessories, and equipment are not opening sources.
 * - Elements in a linked model are supported as read-only sources (Opening Settings > Linked Models).
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 * v1.0.1 (2026-10-12) - Notes only: linked elements are supported sources now, and cable tray fittings
 *                       count as Cable Tray - the old note said both were excluded.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.MepOpenings
{
    public enum MepOpeningElementKind
    {
        Pipe = 0,
        Duct = 1,
        CableTray = 2,
        Conduit = 3
    }
}
