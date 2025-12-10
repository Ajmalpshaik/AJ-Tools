// Tool Name: Filter Pro - State Snapshot
// Description: Captures persistent UI and selection state for the Filter Pro window.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: System.Collections.Generic, Autodesk.Revit.DB
using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace AJTools.Models
{
    internal class FilterProState
    {
        public List<ElementId> CategoryIds { get; set; } = new List<ElementId>();
        public ElementId ParameterId { get; set; }
        public string RuleType { get; set; }
        public List<FilterValueKey> Values { get; set; } = new List<FilterValueKey>();
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public string Separator { get; set; }
        public bool CaseSensitive { get; set; }
        public bool IncludeCategory { get; set; }
        public bool IncludeParameter { get; set; }
        public bool OverrideExisting { get; set; }
        public bool ApplyToActiveView { get; set; } = true;
        public List<ElementId> TargetViewIds { get; set; } = new List<ElementId>();
        public bool ColorProjectionLines { get; set; }
        public bool ColorProjectionPatterns { get; set; }
        public bool ColorCutLines { get; set; }
        public bool ColorCutPatterns { get; set; }
        public bool ColorHalftone { get; set; }
        public bool ApplyGraphics { get; set; }
        public bool PlaceNewFiltersFirst { get; set; } = true;
        public ElementId PatternId { get; set; }
    }
}
