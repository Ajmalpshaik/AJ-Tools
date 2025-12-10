// Tool Name: Filter Pro - Category Item
// Description: Represents a category option for building parameter filters.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB
using Autodesk.Revit.DB;

namespace AJTools.Models
{
    internal class FilterCategoryItem
    {
        public FilterCategoryItem(ElementId id, string name)
        {
            Id = id;
            Name = name;
        }

        public ElementId Id { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }
}
