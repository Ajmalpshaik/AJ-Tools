// Tool Name: Filter Pro - State Tracker
// Description: Manages persisted Filter Pro UI state between window sessions per document.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: System, System.Collections.Generic, System.Linq, Autodesk.Revit.DB, AJTools.Models
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.Services
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
                Separator = string.IsNullOrWhiteSpace(separator) ? "_" : separator,
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
