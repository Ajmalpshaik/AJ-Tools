#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterProStateTracker.cs
 * Purpose       : Tracks and restores last-used Filter Pro UI selections per document across
 *                 window sessions. Resets automatically when the active document changes.
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
 * Dependencies  : Autodesk Revit API, System.Linq
 *
 * Input         : Active Project document; FilterSelection after a successful filter operation
 * Output        : FilterProState persisted in memory for the current document session
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - State is in-memory only — cleared when Revit is closed or the document changes.
 * - Document key is built from PathName when available; falls back to Title + hash for unsaved docs.
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
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.Services.FilterPro
{
    /// <summary>
    /// Tracks last-used selections for Filter Pro and resets when switching documents.
    /// </summary>
    internal sealed class FilterProStateTracker
    {
        private static FilterProState _lastState;
        private static string _lastDocKey;

        public FilterProStateTracker(Document doc)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            string docKey = BuildDocKey(doc);
            if (!string.Equals(_lastDocKey, docKey, StringComparison.OrdinalIgnoreCase))
            {
                _lastDocKey = docKey;
                _lastState = null;
            }
        }

        public FilterProState LastState => _lastState;

        public void Save(FilterSelection selection,
                         string separator,
                         bool applyToActiveView,
                         IList<ElementId> targetViewIds,
                         bool caseSensitive)
        {
            if (selection == null)
                return;

            _lastState = new FilterProState
            {
                CategoryIds = selection.CategoryIds?.ToList() ?? new List<ElementId>(),
                ParameterId = selection.Parameter?.Id,
                RuleType = selection.RuleType,
                Prefix = selection.Prefix ?? string.Empty,
                Suffix = selection.Suffix ?? string.Empty,
                Separator = string.IsNullOrEmpty(separator) ? "_" : separator,
                CaseSensitive = caseSensitive,
                IncludeCategory = selection.IncludeCategory,
                IncludeParameter = selection.IncludeParameter,
                OverrideExisting = selection.OverrideExisting,
                ApplyToActiveView = applyToActiveView,
                TargetViewIds = targetViewIds?.ToList() ?? new List<ElementId>(),
                ColorProjectionLines = selection.ColorProjectionLines,
                ColorProjectionPatterns = selection.ColorProjectionPatterns,
                ColorCutLines = selection.ColorCutLines,
                ColorCutPatterns = selection.ColorCutPatterns,
                ColorHalftone = selection.ColorHalftone,
                PatternId = selection.PatternId,
                PlaceNewFiltersFirst = selection.PlaceNewFiltersFirst,
                ApplyGraphics = selection.ApplyGraphics,
                Values = FilterValueKeyMatcher.BuildValueKeys(selection.Values)
            };
        }

        private static string BuildDocKey(Document doc)
        {
            // At this point doc is guaranteed non-null by the ctor.
            if (!string.IsNullOrWhiteSpace(doc.PathName))
                return doc.PathName;

            return $"{doc.Title}|{doc.GetHashCode()}";
        }
    }
}
