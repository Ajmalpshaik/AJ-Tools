// Tool Name: Filter Pro - Selection Model
// Description: Represents chosen categories, parameters, values, and apply options for filters.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Collections.Generic
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
