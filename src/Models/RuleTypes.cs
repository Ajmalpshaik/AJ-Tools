#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : RuleTypes.cs
 * Purpose       : String constants for all supported filter rule operators, shared between
 *                 the UI (FilterProWindow's rule selector) and the service layer (FilterCreator
 *                 rule-building switch).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : N/A — constants only
 * Output        : N/A — constants only
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - No Revit API dependency; framework-agnostic.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models
{
    internal static class RuleTypes
    {
        public const string EqualsRule = "equals";
        public const string NotEquals = "not_equals";

        public const string Contains = "contains";
        public const string NotContains = "not_contains";

        public const string BeginsWith = "begins_with";
        public const string NotBeginsWith = "not_begins_with";

        public const string EndsWith = "ends_with";
        public const string NotEndsWith = "not_ends_with";

        public const string Greater = "greater";
        public const string GreaterOrEqual = "greater_or_equal";

        public const string Less = "less";
        public const string LessOrEqual = "less_or_equal";

        public const string HasValue = "has_value";
        public const string HasNoValue = "has_no_value";
    }
}
