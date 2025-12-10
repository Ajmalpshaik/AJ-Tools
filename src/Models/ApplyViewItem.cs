// Tool Name: Filter Pro - Apply View Item
// Description: Model representing a selectable view target for applying filters or ranges.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB
using Autodesk.Revit.DB;

namespace AJTools.Models
{
    internal class ApplyViewItem
    {
        public ApplyViewItem(ElementId id, string name, ViewType type)
        {
            Id = id;
            Name = name;
            ViewType = type;
        }

        public ElementId Id { get; }
        public string Name { get; }
        public ViewType ViewType { get; }
        public string Display => $"{Name} ({ViewType})";
        public override string ToString() => Display;
    }
}
