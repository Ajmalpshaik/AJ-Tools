// Tool Name: Filter Pro - Value Key Matcher
// Description: Builds and evaluates value keys for restoring Filter Pro selections.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: System, System.Collections.Generic, Autodesk.Revit.DB, AJTools.Models
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.Services
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
                               eid.IntegerValue == key.ElementIdValue.Value;
                    }

                default:
                    return false;
            }
        }
    }
}
