using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.Purge
{
    internal sealed class FamilyParameterUsageEvaluator
    {
        private const double DoubleValueTolerance = 1e-9;
        private readonly Document _doc;
        private readonly FamilyManager _familyManager;

        public FamilyParameterUsageEvaluator(Document doc, FamilyManager familyManager)
        {
            _doc = doc;
            _familyManager = familyManager;
        }

        public Dictionary<int, HashSet<string>> BuildFormulaReferenceMap(IList<FamilyParameter> parameters)
        {
            var map = new Dictionary<int, HashSet<string>>();
            if (parameters == null || parameters.Count == 0)
            {
                return map;
            }

            var lookup = parameters
                .Where(p => p != null && p.Definition != null && !string.IsNullOrWhiteSpace(p.Definition.Name))
                .ToList();

            foreach (FamilyParameter owner in lookup)
            {
                string formula = string.Empty;
                try
                {
                    formula = owner.Formula ?? string.Empty;
                }
                catch
                {
                    formula = string.Empty;
                }

                if (string.IsNullOrWhiteSpace(formula))
                {
                    continue;
                }

                foreach (FamilyParameter candidate in lookup)
                {
                    if (IsSameParameter(owner, candidate))
                    {
                        continue;
                    }

                    string candidateName = candidate.Definition.Name;
                    if (!FormulaReferencesParameter(formula, candidateName))
                    {
                        continue;
                    }

                    int key = GetParameterIdValue(candidate);
                    if (key == int.MinValue)
                    {
                        continue;
                    }

                    HashSet<string> owners;
                    if (!map.TryGetValue(key, out owners))
                    {
                        owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        map[key] = owners;
                    }

                    owners.Add(owner.Definition.Name);
                }
            }

            return map;
        }

        public Dictionary<int, int> BuildDimensionLabelMap()
        {
            var map = new Dictionary<int, int>();

            var dimensions = new FilteredElementCollector(_doc)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>()
                .ToList();

            foreach (Dimension dimension in dimensions)
            {
                FamilyParameter label = null;
                try
                {
                    label = dimension.FamilyLabel;
                }
                catch
                {
                    label = null;
                }

                int key = GetParameterIdValue(label);
                if (key == int.MinValue)
                {
                    continue;
                }

                int count;
                map.TryGetValue(key, out count);
                map[key] = count + 1;
            }

            return map;
        }

        public AssociatedParameterUsage EvaluateAssociatedUsage(FamilyParameter parameter)
        {
            var result = new AssociatedParameterUsage();
            if (parameter == null)
            {
                return result;
            }

            ParameterSet associated = null;
            try
            {
                associated = parameter.AssociatedParameters;
            }
            catch
            {
                associated = null;
            }

            if (associated == null)
            {
                return result;
            }

            foreach (Parameter linkedParameter in associated)
            {
                if (linkedParameter == null)
                {
                    continue;
                }

                result.AssociatedElementParameterCount++;

                string parameterName = string.Empty;
                try
                {
                    parameterName = linkedParameter.Definition != null ? linkedParameter.Definition.Name : string.Empty;
                }
                catch
                {
                    parameterName = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(parameterName))
                {
                    if (parameterName.IndexOf("visible", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        parameterName.IndexOf("visibility", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.AssociatedWithVisibilityControls = true;
                    }
                }

                Element ownerElement = null;
                try
                {
                    ownerElement = linkedParameter.Element;
                }
                catch
                {
                    ownerElement = null;
                }

                if (ownerElement is FamilyInstance)
                {
                    result.AssociatedWithNestedFamilyParameters = true;
                    result.AssociatedNestedParameterCount++;
                }
            }

            return result;
        }

        public ValueUsageSummary EvaluateValueUsage(FamilyParameter parameter, IList<FamilyType> familyTypes)
        {
            var summary = new ValueUsageSummary();
            if (parameter == null || familyTypes == null || familyTypes.Count == 0)
            {
                return summary;
            }

            FamilyType originalType = null;
            try
            {
                originalType = _familyManager.CurrentType;
            }
            catch
            {
                originalType = null;
            }

            try
            {
                foreach (FamilyType type in familyTypes)
                {
                    if (type == null)
                    {
                        continue;
                    }

                    try
                    {
                        _familyManager.CurrentType = type;
                    }
                    catch
                    {
                        summary.ScanErrors++;
                        continue;
                    }

                    bool hasValue = false;
                    try
                    {
                        hasValue = type.HasValue(parameter);
                    }
                    catch
                    {
                        summary.ScanErrors++;
                        continue;
                    }

                    if (!hasValue)
                    {
                        continue;
                    }

                    summary.HasAnyValue = true;

                    ValueStrength strength = EvaluateValueStrength(type, parameter);
                    if (strength == ValueStrength.Meaningful)
                    {
                        summary.HasMeaningfulValue = true;
                        break;
                    }

                    if (strength == ValueStrength.DefaultLike)
                    {
                        summary.HasDefaultLikeOnlyValue = true;
                    }
                }
            }
            finally
            {
                if (originalType != null)
                {
                    try
                    {
                        _familyManager.CurrentType = originalType;
                    }
                    catch
                    {
                        // Keep scanning safe; failing to restore type should not crash tool.
                    }
                }
            }

            return summary;
        }

        private static ValueStrength EvaluateValueStrength(FamilyType familyType, FamilyParameter parameter)
        {
            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                    {
                        string text = familyType.AsString(parameter);
                        return string.IsNullOrWhiteSpace(text)
                            ? ValueStrength.DefaultLike
                            : ValueStrength.Meaningful;
                    }

                    case StorageType.Integer:
                    {
                        int value = familyType.AsInteger(parameter) ?? 0;
                        bool isYesNo = SharedParamUtils.IsYesNoParameter(parameter.Definition);
                        if (isYesNo)
                        {
                            return value != 0 ? ValueStrength.Meaningful : ValueStrength.DefaultLike;
                        }

                        return value != 0 ? ValueStrength.Meaningful : ValueStrength.DefaultLike;
                    }

                    case StorageType.Double:
                    {
                        double value = familyType.AsDouble(parameter) ?? 0.0;
                        return Math.Abs(value) > DoubleValueTolerance
                            ? ValueStrength.Meaningful
                            : ValueStrength.DefaultLike;
                    }

                    case StorageType.ElementId:
                    {
                        ElementId id = familyType.AsElementId(parameter);
                        return id != null && id != ElementId.InvalidElementId
                            ? ValueStrength.Meaningful
                            : ValueStrength.DefaultLike;
                    }

                    default:
                    {
                        string valueText = familyType.AsValueString(parameter);
                        return string.IsNullOrWhiteSpace(valueText)
                            ? ValueStrength.DefaultLike
                            : ValueStrength.Meaningful;
                    }
                }
            }
            catch
            {
                return ValueStrength.None;
            }
        }

        private static bool FormulaReferencesParameter(string formula, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(formula) || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            // Parameter names with spaces/special chars are usually referenced as plain text in formulas.
            // Contains-check is used for these to preserve compatibility.
            bool hasOnlyWordChars = parameterName.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
            if (!hasOnlyWordChars)
            {
                return formula.IndexOf(parameterName, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            string pattern = @"(?<![A-Za-z0-9_])" + Regex.Escape(parameterName) + @"(?![A-Za-z0-9_])";
            return Regex.IsMatch(formula, pattern, RegexOptions.IgnoreCase);
        }

        private static int GetParameterIdValue(FamilyParameter parameter)
        {
            if (parameter == null || parameter.Id == null || parameter.Id == ElementId.InvalidElementId)
            {
                return int.MinValue;
            }

            return AJTools.Utils.ElementIdHelper.GetIntegerValue(parameter.Id);
        }

        private static bool IsSameParameter(FamilyParameter first, FamilyParameter second)
        {
            int firstId = GetParameterIdValue(first);
            int secondId = GetParameterIdValue(second);

            if (firstId != int.MinValue && secondId != int.MinValue)
            {
                return firstId == secondId;
            }

            return ReferenceEquals(first, second);
        }

        private enum ValueStrength
        {
            None = 0,
            DefaultLike = 1,
            Meaningful = 2
        }
    }

    internal sealed class AssociatedParameterUsage
    {
        public bool AssociatedWithVisibilityControls { get; set; }

        public bool AssociatedWithNestedFamilyParameters { get; set; }

        public int AssociatedNestedParameterCount { get; set; }

        public int AssociatedElementParameterCount { get; set; }
    }

    internal sealed class ValueUsageSummary
    {
        public bool HasAnyValue { get; set; }

        public bool HasMeaningfulValue { get; set; }

        public bool HasDefaultLikeOnlyValue { get; set; }

        public int ScanErrors { get; set; }
    }
}
