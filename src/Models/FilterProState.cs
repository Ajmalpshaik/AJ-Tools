#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterProState.cs
 * Purpose       : Snapshot model that captures all UI and selection state of the Filter Pro
 *                 window for restoration on the next session in the same document.
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
 * Input         : Populated by FilterProStateTracker.Save() after a successful filter operation
 * Output        : Read by FilterProWindow.RestoreLastSelection() on next window open
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - In-memory only — state is not serialised to disk and is lost when Revit closes.
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
    internal class FilterProState
    {
        // Category + Parameter Selection
        public List<ElementId> CategoryIds { get; set; } = new List<ElementId>();
        public ElementId ParameterId { get; set; }
        public string RuleType { get; set; }
        public List<FilterValueKey> Values { get; set; } = new List<FilterValueKey>();

        // Naming Controls
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public string Separator { get; set; }
        public bool CaseSensitive { get; set; }
        public bool IncludeCategory { get; set; }
        public bool IncludeParameter { get; set; }
        public bool OverrideExisting { get; set; }

        // View Targets
        public bool ApplyToActiveView { get; set; } = true;
        public List<ElementId> TargetViewIds { get; set; } = new List<ElementId>();

        // Graphics Controls
        public bool ColorProjectionLines { get; set; }
        public bool ColorProjectionPatterns { get; set; }
        public bool ColorCutLines { get; set; }
        public bool ColorCutPatterns { get; set; }
        public bool ColorHalftone { get; set; }
        public bool ApplyGraphics { get; set; }
        public ElementId PatternId { get; set; }

        // Filter Placement Logic
        public bool PlaceNewFiltersFirst { get; set; } = true;
    }
}
