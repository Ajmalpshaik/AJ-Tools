#region Metadata
/*
 * Tool Name     : AJ Tools Shared Helper
 * File Name     : ElementIdExtensions.cs
 * Purpose       : Version-safe ElementId numeric-value extension methods across Revit 2020 -> 2027.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-07
 * Last Updated  : 2026-07-07
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / .NET Fx 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027 - verify SDK)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : ElementId
 * Output        : Numeric value of the ElementId (int or long), version-safe.
 *
 * Notes         :
 * - Revit 2024 changed ElementId storage from 32-bit to 64-bit: ElementId.IntegerValue was deprecated
 *   (Revit 2024-2025) and REMOVED in Revit 2026 in favour of ElementId.Value (long).
 * - IntValue() reproduces the exact int result call sites relied on before (safe for real project ids and
 *   BuiltInCategory/BuiltInParameter values, all well within int range) by delegating to ElementIdHelper.
 * - LongValue() returns the full 64-bit value for any code that must store or compare ids beyond 32 bits.
 * - The single version branch lives in ElementIdHelper / here so call sites read cleanly as id.IntValue().
 *
 * Changelog     :
 * v1.0.0 (2026-07-07) - Initial release: id.IntValue()/id.LongValue() replacing direct ElementId.IntegerValue.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Version-safe ElementId numeric-value access, replacing direct ElementId.IntegerValue usage
    /// (removed in Revit 2026). Keeps call sites readable: <c>id.IntValue()</c> / <c>id.LongValue()</c>.
    /// </summary>
    internal static class ElementIdExtensions
    {
        /// <summary>
        /// The ElementId numeric value as int, matching the pre-2026 ElementId.IntegerValue behaviour.
        /// Returns the InvalidElementId value when <paramref name="id"/> is null.
        /// </summary>
        internal static int IntValue(this ElementId id) => ElementIdHelper.GetIntegerValue(id);

        /// <summary>
        /// The full 64-bit ElementId numeric value. Use when an id may exceed 32 bits (Revit 2024+).
        /// Returns the InvalidElementId value when <paramref name="id"/> is null.
        /// </summary>
        internal static long LongValue(this ElementId id)
        {
            if (id == null)
                return LongValue(ElementId.InvalidElementId);

#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }
    }
}
