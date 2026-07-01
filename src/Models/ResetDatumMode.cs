#region Metadata
/*
 * Tool Name     : Reset Grid / Level Extents to 3D
 * File Name     : ResetDatumMode.cs
 * Purpose       : Enumerates which datum types (grids, levels, or both) a Reset Datums run targets.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : N/A - enum only
 * Output        : N/A - enum only
 *
 * Notes         :
 * - Targets Revit 2020 through latest; no version-specific API.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.1.0 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest coverage. No logic change.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models
{
    /// <summary>
    /// Specifies which datum types to reset during Reset Datum operations.
    /// </summary>
    internal enum ResetDatumMode
    {
        /// <summary>
        /// Reset both Grids and Levels.
        /// </summary>
        Combined,

        /// <summary>
        /// Reset only Grid elements.
        /// </summary>
        GridsOnly,

        /// <summary>
        /// Reset only Level elements.
        /// </summary>
        LevelsOnly
    }
}
