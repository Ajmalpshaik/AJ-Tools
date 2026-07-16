#region Metadata
/*
 * Tool Name     : AJ-Tools
 * File Name     : ElementIdIntegerComparer.cs
 * Purpose       : IEqualityComparer<ElementId> that compares by IntegerValue so two ElementId
 *                 wrapper objects for the same element are treated as equal in collections.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.1
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Two ElementId instances
 * Output        : Boolean equality and hash code based on IntegerValue
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Must NOT use == operator or GetHashCode() on ElementId directly — Revit may return new
 *   wrapper objects for the same logical element.
 * - IntegerValue is deprecated in Revit 2024+ (replaced by Value returning long); usage here
 *   remains safe since filter IDs are well within int range on all supported versions.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.1.0 (2026-05-25) - Added unchecked guard and null-safety improvements.
 * v1.1.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
            return x.IntValue() == y.IntValue();
        }

        public int GetHashCode(ElementId obj)
        {
            // Hash on IntegerValue to match the Equals contract.
            return obj == null ? 0 : obj.IntValue().GetHashCode();
        }
    }
}
