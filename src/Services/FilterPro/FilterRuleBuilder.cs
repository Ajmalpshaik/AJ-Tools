#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : FilterRuleBuilder.cs
 * Purpose       : Builds Revit FilterRule objects from a parameter, a value, and a rule type by
 *                 storage type. Shared between Filter Pro (wraps rules in a saved view filter) and
 *                 Colorize (runs the same rules live against the active view, no filter created).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-02
 * Last Updated  : 2026-07-02
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : FilterParameterItem, FilterValueItem, rule type string, case-sensitivity flag.
 * Output        : IList<FilterRule> (null/empty when the combination is unsupported).
 *
 * Notes         :
 * - Extracted from FilterCreator.cs (was FilterCreator.BuildRules + 4 private Build*Rules helpers).
 *   Logic is unchanged from the original Filter Pro implementation — this is a pure relocation so
 *   Colorize can call the exact same rule-building code instead of duplicating it.
 * - ParameterFilterRuleFactory string overloads with caseSensitive bool are confirmed valid for 2020-2026.
 *
 * Changelog     :
 * v1.0.0 (2026-07-02) - Extracted from FilterCreator.cs for reuse by the new Colorize tool.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.Services.FilterPro
{
    internal static class FilterRuleBuilder
    {
        internal static IList<FilterRule> BuildRules(
            FilterParameterItem parameter,
            FilterValueItem value,
            string ruleType,
            bool caseSensitive,
            IList<string> skipped)
        {
            if (parameter == null)
                return null;

            if (value.RawValue is Tuple<string, string> familyAndType)
            {
                // Family + Type is a combined virtual parameter requiring two rules joined as AND.
                // caseSensitive is always false in current workflow (Revit filter UI matches this).
                return new List<FilterRule>
                {
                    ParameterFilterRuleFactory.CreateEqualsRule(
                        new ElementId(BuiltInParameter.ALL_MODEL_FAMILY_NAME),
                        familyAndType.Item1,
                        caseSensitive),
                    ParameterFilterRuleFactory.CreateEqualsRule(
                        new ElementId(BuiltInParameter.ALL_MODEL_TYPE_NAME),
                        familyAndType.Item2,
                        caseSensitive)
                };
            }

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return BuildStringRules(parameter.Id, value, ruleType, caseSensitive);
                case StorageType.ElementId:
                    return BuildElementIdRules(parameter.Id, value, ruleType);
                case StorageType.Integer:
                    return BuildIntegerRules(parameter, value, ruleType, skipped);
                case StorageType.Double:
                    return BuildDoubleRules(parameter, value, ruleType, skipped);
                default:
                    return null;
            }
        }

        private static IList<FilterRule> BuildStringRules(
            ElementId paramId,
            FilterValueItem value,
            string ruleType,
            bool caseSensitive)
        {
            string text = value.RawValue as string ?? value.Display ?? string.Empty;
            var singleRules = new List<FilterRule>();

            switch (ruleType)
            {
                case RuleTypes.EqualsRule:
                    singleRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.Contains:
                    singleRules.Add(ParameterFilterRuleFactory.CreateContainsRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.BeginsWith:
                    singleRules.Add(ParameterFilterRuleFactory.CreateBeginsWithRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.EndsWith:
                    singleRules.Add(ParameterFilterRuleFactory.CreateEndsWithRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.NotEquals:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.NotContains:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotContainsRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.NotBeginsWith:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotBeginsWithRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.NotEndsWith:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotEndsWithRule(paramId, text, caseSensitive));
                    break;
                case RuleTypes.HasValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    break;
                case RuleTypes.HasNoValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));
                    break;
                default:
                    return null;
            }

            return singleRules;
        }

        private static IList<FilterRule> BuildElementIdRules(
            ElementId paramId,
            FilterValueItem value,
            string ruleType)
        {
            var singleRules = new List<FilterRule>();
            ElementId id = value.ElementId ?? value.RawValue as ElementId ?? ElementId.InvalidElementId;

            if (ruleType == RuleTypes.HasValue)
            {
                singleRules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
            }
            else if (ruleType == RuleTypes.HasNoValue)
            {
                singleRules.Add(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));
            }
            else
            {
                if (id == ElementId.InvalidElementId)
                    return null;

                if (ruleType == RuleTypes.NotEquals)
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, id));
                else
                    singleRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, id));
            }

            return singleRules;
        }

        private static IList<FilterRule> BuildIntegerRules(
            FilterParameterItem parameter,
            FilterValueItem value,
            string ruleType,
            IList<string> skipped)
        {
            var singleRules = new List<FilterRule>();
            ElementId paramId = parameter.Id;

            if (!TryGetInt(value.RawValue, out int intVal) &&
                ruleType != RuleTypes.HasValue &&
                ruleType != RuleTypes.HasNoValue)
            {
                skipped?.Add($"Invalid integer value for parameter '{parameter.Name}'.");
                return null;
            }

            switch (ruleType)
            {
                case RuleTypes.NotEquals:
                    singleRules.Add(ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, intVal));
                    break;
                case RuleTypes.Greater:
                    singleRules.Add(ParameterFilterRuleFactory.CreateGreaterRule(paramId, intVal));
                    break;
                case RuleTypes.GreaterOrEqual:
                    singleRules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, intVal));
                    break;
                case RuleTypes.Less:
                    singleRules.Add(ParameterFilterRuleFactory.CreateLessRule(paramId, intVal));
                    break;
                case RuleTypes.LessOrEqual:
                    singleRules.Add(ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, intVal));
                    break;
                case RuleTypes.HasValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    break;
                case RuleTypes.HasNoValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));
                    break;
                default:
                    singleRules.Add(ParameterFilterRuleFactory.CreateEqualsRule(paramId, intVal));
                    break;
            }

            return singleRules;
        }

        private static IList<FilterRule> BuildDoubleRules(
            FilterParameterItem parameter,
            FilterValueItem value,
            string ruleType,
            IList<string> skipped)
        {
            var singleRules = new List<FilterRule>();
            ElementId paramId = parameter.Id;
            const double tolerance = 1e-6;

            if (!TryGetDouble(value.RawValue, out double dblVal) &&
                ruleType != RuleTypes.HasValue &&
                ruleType != RuleTypes.HasNoValue)
            {
                skipped?.Add($"Invalid double value for parameter '{parameter.Name}'.");
                return null;
            }

            switch (ruleType)
            {
                case RuleTypes.NotEquals:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.Greater:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateGreaterRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.GreaterOrEqual:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.Less:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateLessRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.LessOrEqual:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, dblVal, tolerance));
                    break;
                case RuleTypes.HasValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId));
                    break;
                case RuleTypes.HasNoValue:
                    singleRules.Add(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId));
                    break;
                default:
                    singleRules.Add(
                        ParameterFilterRuleFactory.CreateEqualsRule(paramId, dblVal, tolerance));
                    break;
            }

            return singleRules;
        }

        private static bool TryGetInt(object raw, out int value)
        {
            if (raw is int i)
            {
                value = i;
                return true;
            }

            return int.TryParse(raw?.ToString(), out value);
        }

        private static bool TryGetDouble(object raw, out double value)
        {
            if (raw is double d)
            {
                value = d;
                return true;
            }

            return double.TryParse(raw?.ToString(), out value);
        }
    }
}
