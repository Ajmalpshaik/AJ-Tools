// Tool Name: Filter Pro - Rule Types
// Description: Constants defining supported rule operator keys for filter creation.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: None

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
