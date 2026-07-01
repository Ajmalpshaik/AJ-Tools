#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : PatternItem.cs
 * Purpose       : Immutable wrapper pairing a FillPatternElement ElementId with its display
 *                 name for binding to the pattern selector in the Filter Pro Apply tab.
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
 * Input         : FillPatternElement collected by FilterProWindow.LoadPatterns()
 * Output        : Bound to the pattern ComboBox in the Apply tab
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Plain data container — no Revit API calls, no side effects.
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

namespace AJTools.Models
{
    internal class PatternItem
    {
        public PatternItem(ElementId id, string name)
        {
            Id = id;
            Name = name;
        }

        public ElementId Id { get; }
        public string Name { get; }

        public override string ToString() => Name;
    }
}
