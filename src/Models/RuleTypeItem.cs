// Tool Name: Filter Pro - Rule Type Item
// Description: Represents an available rule operator and its type compatibility.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: None

namespace AJTools.Models
{
    internal class RuleTypeItem
    {
        public RuleTypeItem(
            string key,
            string label,
            bool enabledForStrings,
            bool enabledForNumbers,
            bool enabledForIds)
        {
            Key = key;
            Label = label;
            EnabledForStrings = enabledForStrings;
            EnabledForNumbers = enabledForNumbers;
            EnabledForIds = enabledForIds;
        }

        public string Key { get; }
        public string Label { get; }
        public bool EnabledForStrings { get; }
        public bool EnabledForNumbers { get; }
        public bool EnabledForIds { get; }

        public override string ToString() => Label;
    }
}
