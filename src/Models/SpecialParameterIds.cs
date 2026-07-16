#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : SpecialParameterIds.cs
 * Purpose       : Defines sentinel ElementId values for virtual parameters that exist only in
 *                 the Filter Pro UI (e.g. the combined "Family and Type" parameter).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : N/A — constants only
 * Output        : N/A — constants only
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - FamilyAndType sentinel uses int.MinValue + 100 to avoid collision with any real ElementId or BuiltInParameter.
 * - These IDs are never passed to the Revit API as real parameter identifiers.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Models
{
    /// <summary>
    /// Defines special virtual parameter identifiers that do not exist in Revit.
    /// These are used only inside the Filter Pro UI for combined or synthetic parameters,
    /// such as "Family + Type". Values must never collide with built-in or real parameter IDs.
    /// </summary>
    internal static class SpecialParameterIds
    {
        /// <summary>
        /// Sentinel ID used to represent the combined "Family + Type" virtual parameter.
        /// Uses a very low negative number to avoid collision with real ElementIds.
        /// </summary>
        public static readonly ElementId FamilyAndType =
            ElementIdHelper.FromInt(int.MinValue + 100);
    }
}
