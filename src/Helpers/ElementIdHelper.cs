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
 * - Revit 2024 also widened the BuiltInCategory/BuiltInParameter enums from Int32 to Int64. Calling
 *   Enum.IsDefined(typeof(BuiltInCategory), someInt) with a boxed `int` on 2024+ THROWS
 *   "Enum underlying type and the object must be same type..." because Enum.IsDefined requires the
 *   boxed value's type to exactly match the enum's underlying type. IsDefinedBuiltInCategory/
 *   IsDefinedBuiltInParameter box using the enum's actual (reflected) underlying type, so they are
 *   correct regardless of which Revit version's enum width is active - no version #if needed.
 * - ElementId(int) itself was deprecated in Revit 2024 (when ElementId(long) was added) and
 *   CONFIRMED REMOVED by Revit 2027 (checked against the real installed RevitAPI.dll, not just
 *   docs - only BuiltInParameter/BuiltInCategory/Int64 constructors remain). FromInt(int) switches
 *   to the long constructor starting 2024 (matching GetIntegerValue's own switch point above), so
 *   it is correct for the entire 2024-2027 range regardless of exactly which point release actually
 *   dropped the int overload - never call `new ElementId(someInt)` directly at a call site.
 *
 * Changelog     :
 * v1.0.0 (2026-06-27) - Initial release.
 * v1.1.0 (2026-07-07) - Added IsDefinedBuiltInCategory/IsDefinedBuiltInParameter to fix an
 *                       Enum.IsDefined boxing-type mismatch that threw in Revit 2024+.
 * v1.2.0 (2026-07-07) - Added FromInt(int) - ElementId(int) constructor confirmed removed in
 *                       Revit 2027 (checked against the real installed API, not just docs); every
 *                       `new ElementId(someInt)` call site in the codebase now routes through this.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
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

        /// <summary>
        /// True if <paramref name="categoryId"/> matches a defined BuiltInCategory value, version-safe.
        /// Replaces the unsafe <c>Enum.IsDefined(typeof(BuiltInCategory), someInt)</c>, which throws in
        /// Revit 2024+ because BuiltInCategory's underlying type widened from Int32 to Int64.
        /// </summary>
        internal static bool IsDefinedBuiltInCategory(int categoryId)
        {
            return IsDefinedEnumValue(typeof(BuiltInCategory), categoryId);
        }

        /// <summary>
        /// True if <paramref name="parameterId"/> matches a defined BuiltInParameter value, version-safe.
        /// Replaces the unsafe <c>Enum.IsDefined(typeof(BuiltInParameter), someInt)</c>, which throws in
        /// Revit 2024+ because BuiltInParameter's underlying type widened from Int32 to Int64.
        /// </summary>
        internal static bool IsDefinedBuiltInParameter(int parameterId)
        {
            return IsDefinedEnumValue(typeof(BuiltInParameter), parameterId);
        }

        private static bool IsDefinedEnumValue(Type enumType, int value)
        {
            Type underlyingType = Enum.GetUnderlyingType(enumType);
            object boxedValue = Convert.ChangeType(value, underlyingType);
            return Enum.IsDefined(enumType, boxedValue);
        }

        /// <summary>
        /// Version-safe replacement for <c>new ElementId(int)</c>, which no longer exists on Revit
        /// 2027 (confirmed against the real installed API). Never construct an ElementId from a plain
        /// int directly at a call site - always go through this method.
        /// </summary>
        internal static ElementId FromInt(int value)
        {
#if REVIT2024_OR_GREATER
            return new ElementId((long)value);
#else
            return new ElementId(value);
#endif
        }
    }
}
