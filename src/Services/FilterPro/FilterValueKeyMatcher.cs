#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterValueKeyMatcher.cs
 * Purpose       : Builds composite FilterValueKey objects from UI selections and evaluates
 *                 whether a given FilterValueItem matches a saved key, enabling session restore.
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
 * Dependencies  : Autodesk Revit API, System.Collections.Generic
 *
 * Input         : IList<FilterValueItem> (selected values from UI); IList<FilterValueKey> (saved keys)
 * Output        : List<FilterValueKey> for persistence; boolean match result for restore
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Family+Type keys are encoded as "__FAMILY_AND_TYPE__<family>|||<type>" to avoid collision.
 * - Double comparison uses 1e-6 tolerance to handle floating-point storage differences.
 * - Production-ready implementation. No model changes.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models;

using AJTools.Utils;
namespace AJTools.Services.FilterPro
{
    /// <summary>
    /// Centralizes value-key creation and comparisons so restoration logic is reusable.
    /// </summary>
    internal static class FilterValueKeyMatcher
    {
        private const string FamilyTypePrefix = "__FAMILY_AND_TYPE__";
        private const string FamilyTypeSeparator = "|||";

        public static List<FilterValueKey> BuildValueKeys(IList<FilterValueItem> selectedValues)
        {
            var keys = new List<FilterValueKey>();
            if (selectedValues == null)
                return keys;

            foreach (var v in selectedValues)
            {
                if (v == null)
                    continue;

                if (v.RawValue is Tuple<string, string> familyAndType)
                {
                    string key = $"{FamilyTypePrefix}{familyAndType.Item1}{FamilyTypeSeparator}{familyAndType.Item2}";
                    keys.Add(FilterValueKey.ForString(key));
                }
                else if (v.StorageType == StorageType.String)
                {
                    string s = v.RawValue as string ?? v.Display;
                    if (!string.IsNullOrWhiteSpace(s))
                        keys.Add(FilterValueKey.ForString(s));
                }
                else if (v.StorageType == StorageType.Integer)
                {
                    int i = Convert.ToInt32(v.RawValue);
                    keys.Add(FilterValueKey.ForInt(i));
                }
                else if (v.StorageType == StorageType.Double)
                {
                    double d = Convert.ToDouble(v.RawValue);
                    keys.Add(FilterValueKey.ForDouble(d));
                }
                else if (v.StorageType == StorageType.ElementId)
                {
                    ElementId eid = v.ElementId ?? v.RawValue as ElementId ?? ElementId.InvalidElementId;
                    if (eid != ElementId.InvalidElementId)
                        keys.Add(FilterValueKey.ForElementId(eid));
                }
            }

            return keys;
        }

        public static bool MatchesValue(FilterValueItem item, IList<FilterValueKey> keys)
        {
            if (item == null || keys == null || keys.Count == 0)
                return false;

            foreach (var key in keys)
            {
                if (key == null || key.StorageType != item.StorageType)
                    continue;

                if (IsFamilyAndTypeKey(key))
                {
                    if (MatchesFamilyAndType(item, key))
                        return true;
                }
                else if (MatchesSimpleValue(item, key))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFamilyAndTypeKey(FilterValueKey key)
        {
            return key.StorageType == StorageType.String &&
                   key.StringValue != null &&
                   key.StringValue.StartsWith(FamilyTypePrefix, StringComparison.Ordinal);
        }

        private static bool MatchesFamilyAndType(FilterValueItem item, FilterValueKey key)
        {
            if (!(item.RawValue is Tuple<string, string> familyAndType))
                return false;

            string savedKey = key.StringValue;
            if (string.IsNullOrEmpty(savedKey) || savedKey.Length <= FamilyTypePrefix.Length)
                return false;

            string content = savedKey.Substring(FamilyTypePrefix.Length);
            var parts = content.Split(new[] { FamilyTypeSeparator }, StringSplitOptions.None);
            if (parts.Length != 2)
                return false;

            string savedFamily = parts[0];
            string savedType = parts[1];

            return string.Equals(familyAndType.Item1, savedFamily, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(familyAndType.Item2, savedType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesSimpleValue(FilterValueItem item, FilterValueKey key)
        {
            switch (key.StorageType)
            {
                case StorageType.String:
                    {
                        string itemStr = item.RawValue as string ?? item.Display;
                        return !string.IsNullOrEmpty(itemStr) &&
                               !string.IsNullOrEmpty(key.StringValue) &&
                               string.Equals(itemStr, key.StringValue, StringComparison.OrdinalIgnoreCase);
                    }

                case StorageType.Integer:
                    {
                        if (!key.IntValue.HasValue || item.RawValue == null)
                            return false;

                        int itemInt = Convert.ToInt32(item.RawValue);
                        return itemInt == key.IntValue.Value;
                    }

                case StorageType.Double:
                    {
                        if (!key.DoubleValue.HasValue || item.RawValue == null)
                            return false;

                        double itemDouble = Convert.ToDouble(item.RawValue);
                        return Math.Abs(itemDouble - key.DoubleValue.Value) < 1e-6;
                    }

                case StorageType.ElementId:
                    {
                        if (!key.ElementIdValue.HasValue)
                            return false;

                        ElementId eid = item.ElementId ?? item.RawValue as ElementId ?? ElementId.InvalidElementId;
                        return eid != null && eid != ElementId.InvalidElementId &&
                               eid.IntValue() == key.ElementIdValue.Value;
                    }

                default:
                    return false;
            }
        }
    }
}
