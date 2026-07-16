// Tool Name: Shared Parameter to Family Parameter - Service
// Description: Converts selected shared family parameters to normal family parameters.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-26
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;
using AJTools.Models;
using AJTools.Utils;

// Version-safe token type: BuiltInParameterGroup on Revit 2020-2021, ForgeTypeId on Revit 2022+.
#if REVIT2022_OR_GREATER
using AjGroup = Autodesk.Revit.DB.ForgeTypeId;
#else
using AjGroup = Autodesk.Revit.DB.BuiltInParameterGroup;
#endif

namespace AJTools.Services
{
    internal sealed class SharedParamToFamilyParamService
    {
        private readonly Document _doc;
        private readonly FamilyManager _familyManager;

        public SharedParamToFamilyParamService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _familyManager = _doc.FamilyManager;
        }

        public IList<SharedParamToFamilyParamItem> GetSharedParameters()
        {
            var items = new List<SharedParamToFamilyParamItem>();
            IList<FamilyParameter> sharedParameters = SharedParamUtils.GetSharedParameters(_familyManager);

            for (int i = 0; i < sharedParameters.Count; i++)
            {
                var parameter = sharedParameters[i];
                if (parameter == null)
                {
                    continue;
                }

                items.Add(new SharedParamToFamilyParamItem(parameter));
            }

            items.Sort((a, b) => string.Compare(a?.Name, b?.Name, StringComparison.CurrentCultureIgnoreCase));
            return items;
        }

        public SharedParamToFamilyParamResult Convert(IList<SharedParamToFamilyParamItem> itemsToConvert)
        {
            var result = new SharedParamToFamilyParamResult();
            if (itemsToConvert == null || itemsToConvert.Count == 0)
            {
                return result;
            }

            if (_doc.IsReadOnly)
            {
                result.AddFailure(string.Empty, "The current family document is read-only.");
                return result;
            }

            using (var group = new TransactionGroup(_doc, "Shared Parameter to Family Parameter"))
            {
                group.Start();

                bool hasCommittedChanges = false;
                for (int i = 0; i < itemsToConvert.Count; i++)
                {
                    var item = itemsToConvert[i];
                    string parameterName = item?.Name ?? "Unknown Parameter";

                    using (var transaction = new Transaction(_doc, $"Convert '{parameterName}'"))
                    {
                        transaction.Start();

                        try
                        {
                            if (TryConvertSingle(item, result))
                            {
                                transaction.Commit();
                                hasCommittedChanges = true;
                            }
                            else
                            {
                                transaction.RollBack();
                            }
                        }
                        catch (Exception ex)
                        {
                            if (transaction.GetStatus() != TransactionStatus.RolledBack && transaction.GetStatus() != TransactionStatus.Committed)
                            {
                                transaction.RollBack();
                            }
                            result.AddFailure(parameterName, $"Unexpected error: {ex.Message}");
                        }
                    }
                }

                if (hasCommittedChanges)
                {
                    group.Assimilate();
                }
                else
                {
                    group.RollBack();
                }
            }

            return result;
        }

        private bool TryConvertSingle(SharedParamToFamilyParamItem item, SharedParamToFamilyParamResult result)
        {
            if (item == null)
            {
                result.AddFailure(string.Empty, "A selected parameter was invalid.");
                return false;
            }

            FamilyParameter sourceParameter = SharedParamUtils.FindSharedParameter(_familyManager, item);
            if (sourceParameter == null)
            {
                result.AddFailure(item.Name, "Parameter was not found in the family or is no longer shared.");
                return false;
            }

            if (sourceParameter.IsReadOnly)
            {
                result.AddFailure(item.Name, "Parameter is read-only and cannot be replaced.");
                return false;
            }

            Definition definition = sourceParameter.Definition;
            if (definition == null || string.IsNullOrWhiteSpace(definition.Name))
            {
                result.AddFailure(item.Name, "Parameter definition is missing.");
                return false;
            }

            string parameterName = definition.Name;
            AjGroup parameterGroup = RevitCompat.GetGroup(definition);
            bool isInstance = sourceParameter.IsInstance;
            bool wasReporting = SharedParamUtils.IsReporting(sourceParameter);
            string formula = SharedParamUtils.GetFormula(sourceParameter);

            var warnings = new List<string>();
            IList<SharedParamUtils.FamilyTypeValueSnapshot> valuesByType = SharedParamUtils.CaptureValues(
                _familyManager,
                sourceParameter,
                warnings);

            if (HasConflictingName(sourceParameter, parameterName))
            {
                result.AddFailure(
                    parameterName,
                    $"A different family parameter already uses the name '{parameterName}'. Rename or remove the conflicting parameter and try again.");
                return false;
            }

            if (!TryReplaceSharedParameter(
                sourceParameter,
                parameterName,
                parameterGroup,
                isInstance,
                warnings,
                out FamilyParameter convertedParameter,
                out string replacementError))
            {
                result.AddFailure(parameterName, replacementError);
                return false;
            }

            if (convertedParameter == null)
            {
                result.AddFailure(parameterName, "Revit did not return a replacement parameter.");
                return false;
            }

            if (convertedParameter.IsShared)
            {
                result.AddFailure(parameterName, "Conversion failed because the new parameter is still shared.");
                return false;
            }

            bool formulaRestored = SharedParamUtils.TryRestoreFormula(
                _familyManager,
                convertedParameter,
                formula,
                warnings);

            if (!formulaRestored || string.IsNullOrWhiteSpace(formula))
            {
                SharedParamUtils.RestoreValues(
                    _familyManager,
                    convertedParameter,
                    valuesByType,
                    warnings);
            }

            SharedParamUtils.TryRestoreReportingState(
                _familyManager,
                convertedParameter,
                wasReporting,
                warnings);

            result.AddSuccess(parameterName);
            for (int i = 0; i < warnings.Count; i++)
            {
                result.AddWarning(parameterName, warnings[i]);
            }

            return true;
        }

        private bool TryReplaceSharedParameter(
            FamilyParameter sourceParameter,
            string targetName,
            AjGroup parameterGroup,
            bool isInstance,
            IList<string> warnings,
            out FamilyParameter convertedParameter,
            out string errorMessage)
        {
            convertedParameter = null;
            errorMessage = string.Empty;

            try
            {
                convertedParameter = RevitCompat.ReplaceParameter(
                    _familyManager,
                    sourceParameter,
                    targetName,
                    parameterGroup,
                    isInstance);
                return convertedParameter != null;
            }
            catch (Exception ex)
            {
                if (!IsNameAlreadyInUseError(ex))
                {
                    errorMessage = $"Replacement failed: {CleanExceptionMessage(ex.Message)}";
                    return false;
                }
            }

            string tempName = BuildUniqueTemporaryName(targetName);
            try
            {
                convertedParameter = _familyManager.ReplaceParameter(
                    sourceParameter,
                    tempName,
                    parameterGroup,
                    isInstance);

                if (convertedParameter == null)
                {
                    errorMessage = "Revit did not return a replacement parameter.";
                    return false;
                }

                _familyManager.RenameParameter(convertedParameter, targetName);
                warnings?.Add(
                    $"Revit reported '{targetName}' as already in use. Applied a safe temporary replace+rename fallback.");
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Replacement failed: {CleanExceptionMessage(ex.Message)}";
                return false;
            }
        }

        private bool HasConflictingName(FamilyParameter sourceParameter, string targetName)
        {
            if (sourceParameter == null || string.IsNullOrWhiteSpace(targetName))
            {
                return false;
            }

            FamilyParameterSet allParameters = _familyManager?.Parameters;
            if (allParameters == null)
            {
                return false;
            }

            FamilyParameterSetIterator iterator = allParameters.ForwardIterator();
            iterator.Reset();
            while (iterator.MoveNext())
            {
                var parameter = iterator.Current as FamilyParameter;
                if (parameter == null || IsSameParameter(parameter, sourceParameter))
                {
                    continue;
                }

                string existingName = parameter.Definition?.Name;
                if (!string.IsNullOrWhiteSpace(existingName) &&
                    existingName.Equals(targetName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool DoesParameterNameExist(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            FamilyParameterSet allParameters = _familyManager?.Parameters;
            if (allParameters == null)
            {
                return false;
            }

            FamilyParameterSetIterator iterator = allParameters.ForwardIterator();
            iterator.Reset();
            while (iterator.MoveNext())
            {
                var parameter = iterator.Current as FamilyParameter;
                string existingName = parameter?.Definition?.Name;
                if (!string.IsNullOrWhiteSpace(existingName) &&
                    existingName.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildUniqueTemporaryName(string baseName)
        {
            string safeBaseName = string.IsNullOrWhiteSpace(baseName) ? "AJTempParam" : baseName.Trim();
            if (safeBaseName.Length > 45)
            {
                safeBaseName = safeBaseName.Substring(0, 45);
            }

            int index = 1;
            while (true)
            {
                string candidate = $"{safeBaseName}_AJTMP_{index}";
                if (!DoesParameterNameExist(candidate))
                {
                    return candidate;
                }

                index++;
            }
        }

        private static bool IsNameAlreadyInUseError(Exception exception)
        {
            string message = exception?.Message ?? string.Empty;
            return message.IndexOf("already in use", StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static string CleanExceptionMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Unknown Revit API error.";
            }

            string cleaned = message.Replace("Parameter name: parameterName", string.Empty);
            return cleaned.Trim();
        }

        private static bool IsSameParameter(FamilyParameter first, FamilyParameter second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            if (first.Id != null && second.Id != null && first.Id != ElementId.InvalidElementId && second.Id != ElementId.InvalidElementId)
            {
                return first.Id.IntValue() == second.Id.IntValue();
            }

            return ReferenceEquals(first, second);
        }
    }

    internal sealed class SharedParamToFamilyParamResult
    {
        private readonly List<string> _successes = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _failures = new List<string>();

        public int SuccessCount => _successes.Count;

        public int WarningCount => _warnings.Count;

        public int FailureCount => _failures.Count;

        public IList<string> Successes => _successes;

        public IList<string> Warnings => _warnings;

        public IList<string> Failures => _failures;

        public void AddSuccess(string parameterName)
        {
            string name = string.IsNullOrWhiteSpace(parameterName) ? "Unnamed Parameter" : parameterName;
            _successes.Add(name);
        }

        public void AddWarning(string parameterName, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string name = string.IsNullOrWhiteSpace(parameterName) ? "Unnamed Parameter" : parameterName;
            _warnings.Add($"{name}: {message}");
        }

        public void AddFailure(string parameterName, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string name = string.IsNullOrWhiteSpace(parameterName) ? "Unnamed Parameter" : parameterName;
            _failures.Add($"{name}: {message}");
        }

        public string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Converted: {SuccessCount}");
            sb.AppendLine($"Warnings: {WarningCount}");
            sb.AppendLine($"Skipped/Failed: {FailureCount}");

            AppendSection(sb, "Converted Parameters", _successes, 20);
            AppendSection(sb, "Warnings", _warnings, 20);
            AppendSection(sb, "Skipped/Failed Parameters", _failures, 20);

            return sb.ToString().Trim();
        }

        private static void AppendSection(StringBuilder sb, string title, IList<string> lines, int maxLines)
        {
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine(title + ":");

            int count = lines.Count < maxLines ? lines.Count : maxLines;
            for (int i = 0; i < count; i++)
            {
                sb.AppendLine("- " + lines[i]);
            }

            if (lines.Count > maxLines)
            {
                sb.AppendLine($"- ... and {lines.Count - maxLines} more.");
            }
        }
    }
}
