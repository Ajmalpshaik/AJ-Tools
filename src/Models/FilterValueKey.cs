// Tool Name: Filter Pro - Value Key
// Description: Composite key representing parameter value identity and display formatting.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB
using Autodesk.Revit.DB;

namespace AJTools.Models
{
    internal class FilterValueKey
    {
        public StorageType StorageType { get; private set; }
        public string StringValue { get; private set; }
        public int? IntValue { get; private set; }
        public double? DoubleValue { get; private set; }
        public int? ElementIdValue { get; private set; }

        private FilterValueKey() { }

        public static FilterValueKey ForString(string value) =>
            new FilterValueKey { StorageType = StorageType.String, StringValue = value };

        public static FilterValueKey ForInt(int value) =>
            new FilterValueKey { StorageType = StorageType.Integer, IntValue = value };

        public static FilterValueKey ForDouble(double value) =>
            new FilterValueKey { StorageType = StorageType.Double, DoubleValue = value };

        public static FilterValueKey ForElementId(ElementId id) =>
            new FilterValueKey
            {
                StorageType = StorageType.ElementId,
                ElementIdValue = id?.IntegerValue
            };
    }
}
