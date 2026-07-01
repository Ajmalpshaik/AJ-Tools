#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : RuleTypeItem.cs
 * Purpose       : Represents a rule operator (equals, contains, greater, etc.) and flags
 *                 which StorageTypes it is valid for, driving UI enable/disable logic.
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
 * Input         : Constructed by FilterProWindow when populating the rule type radio/combo options
 * Output        : Bound to the rule type selector in the Configuration tab
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
