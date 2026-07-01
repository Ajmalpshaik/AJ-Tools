#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterSelection.cs
 * Purpose       : Runtime model representing the user's complete selection — categories,
 *                 parameter, values, rule type, naming options, graphics settings, and view targets.
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
 * Dependencies  : Autodesk Revit API, System.Collections.Generic
 *
 * Input         : Populated by FilterProWindow before passing to FilterCreator and FilterApplier
 * Output        : Consumed by FilterCreator, FilterApplier, FilterReorderer, FilterProStateTracker
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
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
using System.Collections.Generic;

namespace AJTools.Models
{
    internal class FilterSelection
    {
        public IList<ElementId> CategoryIds { get; set; }

        public FilterParameterItem Parameter { get; set; }

        public IList<FilterValueItem> Values { get; set; }

        public string RuleType { get; set; }

        public bool ApplyToView { get; set; }

        public bool ApplyToActiveView { get; set; }

        public IList<ElementId> TargetViewIds { get; set; }

        public bool OverrideExisting { get; set; }

        public bool RandomColors { get; set; }

        public bool ColorProjectionLines { get; set; }

        public bool ColorProjectionPatterns { get; set; }

        public bool ColorCutLines { get; set; }

        public bool ColorCutPatterns { get; set; }

        public bool ColorHalftone { get; set; }

        public bool ApplyGraphics { get; set; }

        public ElementId PatternId { get; set; }

        public bool PlaceNewFiltersFirst { get; set; } = true;

        public string Prefix { get; set; }

        public string Suffix { get; set; }

        public string Separator { get; set; }

        public bool CaseSensitive { get; set; }

        public bool IncludeCategory { get; set; }

        public bool IncludeParameter { get; set; }
    }
}
