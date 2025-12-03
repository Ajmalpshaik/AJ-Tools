using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools
{
    /// <summary>
    /// Comparer that treats ElementIds equal when their IntegerValue matches.
    /// Useful for HashSet/Dictionary with ElementIds.
    /// </summary>
    internal class ElementIdIntegerComparer : IEqualityComparer<ElementId>
    {
        public bool Equals(ElementId x, ElementId y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.IntegerValue == y.IntegerValue;
        }

        public int GetHashCode(ElementId obj)
        {
            return obj?.IntegerValue ?? 0;
        }
    }
}
