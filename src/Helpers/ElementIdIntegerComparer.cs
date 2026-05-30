// Tool Name: ElementId Integer Comparer
// Description: Provides integer-based value comparison for ElementId hashing and equality.
// Compares by IntegerValue so that two ElementId objects with the same ID are treated as equal,
// regardless of object reference (required because Revit API may create new ElementId wrappers).
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2026-05-25
// Revit Version: 2020+
// Dependencies: Autodesk.Revit.DB
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Comparer that treats ElementIds as equal when their IntegerValue matches.
    /// Must NOT use the == operator or GetHashCode() on ElementId directly because
    /// Revit may return different wrapper objects for the same logical element.
    /// </summary>
    internal class ElementIdIntegerComparer : IEqualityComparer<ElementId>
    {
        public bool Equals(ElementId x, ElementId y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x == null || y == null)
                return false;

            // Compare by integer value — the only reliable identity for ElementIds.
            return x.IntegerValue == y.IntegerValue;
        }

        public int GetHashCode(ElementId obj)
        {
            // Hash on IntegerValue to match the Equals contract.
            return obj == null ? 0 : obj.IntegerValue.GetHashCode();
        }
    }
}
