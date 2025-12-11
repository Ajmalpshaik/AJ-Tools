// Tool Name: Filter Pro - Parameter Item
// Description: Represents a parameter and its metadata for filter selection.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using Autodesk.Revit.DB;

namespace AJTools.Models
{
    internal class FilterParameterItem
    {
        public FilterParameterItem(ElementId id, string name, StorageType storageType)
        {
            Id = id;
            Name = name;
            StorageType = storageType;
        }

        public ElementId Id { get; }
        public string Name { get; }
        public StorageType StorageType { get; }

        public override string ToString() => Name;
    }
}
