#region Metadata
/*
 * Tool Name     : AJ Tools Shared Helper
 * File Name     : FilterRuleCompat.cs
 * Purpose       : Version-safe ParameterFilterRuleFactory string rules across Revit 2020 -> 2027.
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
 * Input         : Parameter ElementId, comparison text, requested case sensitivity.
 * Output        : A version-correct string FilterRule.
 *
 * Notes         :
 * - Revit 2020-2022: string rule factory methods take (ElementId, string, bool caseSensitive).
 * - Revit 2023: string comparisons became case-insensitive only; a 2-argument (ElementId, string)
 *   overload was added and the 3-argument overload deprecated, then REMOVED in Revit 2026.
 * - AJ-Tools always requested case-insensitive matching (caseSensitive = false), so dropping the flag
 *   on 2023+ preserves the exact matching behaviour. The requested flag is still honoured on 2020-2022.
 * - The version branch lives ONLY here so FilterPro rule building stays one clean code path.
 *
 * Changelog     :
 * v1.0.0 (2026-07-07) - Initial release: string FilterRule factory bridged for Revit 2020 -> 2027.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Bridges the ParameterFilterRuleFactory string-rule methods whose case-sensitivity argument
    /// was removed in Revit 2023 (and the old 3-argument overload deleted in Revit 2026).
    /// </summary>
    internal static class FilterRuleCompat
    {
        internal static FilterRule Equals(ElementId parameter, string value, bool caseSensitive)
#if REVIT2023_OR_GREATER
            => ParameterFilterRuleFactory.CreateEqualsRule(parameter, value);
#else
            => ParameterFilterRuleFactory.CreateEqualsRule(parameter, value, caseSensitive);
#endif

        internal static FilterRule NotEquals(ElementId parameter, string value, bool caseSensitive)
#if REVIT2023_OR_GREATER
            => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, value);
#else
            => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, value, caseSensitive);
#endif

        internal static FilterRule Contains(ElementId parameter, string value, bool caseSensitive)
#if REVIT2023_OR_GREATER
            => ParameterFilterRuleFactory.CreateContainsRule(parameter, value);
#else
            => ParameterFilterRuleFactory.CreateContainsRule(parameter, value, caseSensitive);
#endif

        internal static FilterRule NotContains(ElementId parameter, string value, bool caseSensitive)
#if REVIT2023_OR_GREATER
            => ParameterFilterRuleFactory.CreateNotContainsRule(parameter, value);
#else
            => ParameterFilterRuleFactory.CreateNotContainsRule(parameter, value, caseSensitive);
#endif

        internal static FilterRule BeginsWith(ElementId parameter, string value, bool caseSensitive)
#if REVIT2023_OR_GREATER
            => ParameterFilterRuleFactory.CreateBeginsWithRule(parameter, value);
#else
            => ParameterFilterRuleFactory.CreateBeginsWithRule(parameter, value, caseSensitive);
#endif

        internal static FilterRule NotBeginsWith(ElementId parameter, string value, bool caseSensitive)
#if REVIT2023_OR_GREATER
            => ParameterFilterRuleFactory.CreateNotBeginsWithRule(parameter, value);
#else
            => ParameterFilterRuleFactory.CreateNotBeginsWithRule(parameter, value, caseSensitive);
#endif

        internal static FilterRule EndsWith(ElementId parameter, string value, bool caseSensitive)
#if REVIT2023_OR_GREATER
            => ParameterFilterRuleFactory.CreateEndsWithRule(parameter, value);
#else
            => ParameterFilterRuleFactory.CreateEndsWithRule(parameter, value, caseSensitive);
#endif

        internal static FilterRule NotEndsWith(ElementId parameter, string value, bool caseSensitive)
#if REVIT2023_OR_GREATER
            => ParameterFilterRuleFactory.CreateNotEndsWithRule(parameter, value);
#else
            => ParameterFilterRuleFactory.CreateNotEndsWithRule(parameter, value, caseSensitive);
#endif
    }
}
