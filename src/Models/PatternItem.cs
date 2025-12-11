// Tool Name: Filter Pro - Pattern Item
// Description: Represents a fill pattern option for filter overrides.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

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
