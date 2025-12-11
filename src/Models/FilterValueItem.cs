// Tool Name: Filter Pro - Value Item
// Description: Holds a parameter value and display info for filter rule creation.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using Autodesk.Revit.DB;

namespace AJTools.Models
{
    internal class FilterValueItem
    {
        public FilterValueItem(
            string display,
            object rawValue,
            StorageType storageType,
            ElementId elementId = null)
        {
            Display = display;
            RawValue = rawValue;
            StorageType = storageType;
            ElementId = elementId;
        }

        public string Display { get; }
        public object RawValue { get; }
        public StorageType StorageType { get; }
        public ElementId ElementId { get; }

        public override string ToString() => Display;
    }
}
