// Tool Name: ElementId Integer Comparer
// Description: Provides integer-based comparison for ElementId hashing and equality.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Comparer that treats ElementIds as equal when their IntegerValue matches.
    /// Useful for HashSet/Dictionary with ElementIds.
    /// </summary>
    internal class ElementIdIntegerComparer : IEqualityComparer<ElementId>
    {
        public bool Equals(ElementId x, ElementId y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x == null || y == null)
                return false;

            return x.IntegerValue == y.IntegerValue;
        }

        public int GetHashCode(ElementId obj)
        {
            // ElementId is a struct; null will never occur here unless caller passes null explicitly.
            return obj != null ? obj.IntegerValue : 0;
        }
    }
}
