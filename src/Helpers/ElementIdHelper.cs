#region Metadata
/*
 * Tool Name     : AJ Tools Shared Helper
 * File Name     : ElementIdHelper.cs
 * Purpose       : Version-safe access to Revit ElementId numeric value across Revit 2020 -> latest.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-06-27
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : ElementId
 * Output        : Integer/string representation of the ElementId, validity check.
 *
 * Notes         :
 * - Revit 2024+ deprecated ElementId.IntegerValue in favour of ElementId.Value (long).
 * - This helper centralises the difference so call sites never branch on Revit version.
 * - Under the current build (Revit 2020) the helper compiles to ElementId.IntegerValue.
 * - Under future Revit 2024+ builds (REVIT2024_OR_GREATER defined) it compiles to (int)ElementId.Value.
 * - Casting long -> int is safe for normal Revit project ElementIds (well below int.MaxValue).
 *
 * Changelog     :
 * v1.0.0 (2026-06-27) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Provides version-safe access to ElementId numeric value across Revit 2020 -> latest.
    /// </summary>
    internal static class ElementIdHelper
    {
        /// <summary>
        /// Returns the numeric value of an ElementId as int. Returns the InvalidElementId
        /// numeric value if <paramref name="id"/> is null.
        /// </summary>
        internal static int GetIntegerValue(ElementId id)
        {
            if (id == null)
                return GetIntegerValue(ElementId.InvalidElementId);

#if REVIT2024_OR_GREATER
            return (int)id.Value;
#else
            return id.IntegerValue;
#endif
        }

        /// <summary>
        /// Returns a printable string representation of an ElementId numeric value
        /// suitable for diagnostic reports.
        /// </summary>
        internal static string ToReportString(ElementId id)
        {
            if (id == null)
                return "(none)";

            return GetIntegerValue(id).ToString();
        }

        /// <summary>
        /// True if the ElementId is non-null and not equal to <see cref="ElementId.InvalidElementId"/>.
        /// </summary>
        internal static bool IsValid(ElementId id)
        {
            return id != null && id != ElementId.InvalidElementId;
        }
    }
}
