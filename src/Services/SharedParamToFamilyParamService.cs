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
            IList<FamilyParameter> sharedParameters = CollectSharedFamilyParameters(_familyManager);

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

            FamilyParameter sourceParameter = FindSharedParameter(_familyManager, item);
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
            string formula = GetFormula(sourceParameter);

            var warnings = new List<string>();
            IList<FamilyTypeValueSnapshot> valuesByType = CaptureValues(
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

            bool formulaRestored = TryRestoreFormula(
                _familyManager,
                convertedParameter,
                formula,
                warnings);

            if (!formulaRestored || string.IsNullOrWhiteSpace(formula))
            {
                RestoreValues(
                    _familyManager,
                    convertedParameter,
                    valuesByType,
                    warnings);
            }

            TryRestoreReportingState(
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

        // ─────────────────────────────────────────────────────────────────
        // Snapshot/restore algorithm - feature-specific to this conversion, previously duplicated
        // in Helpers/SharedParamUtils.cs where it had no other consumer. Moved here (code review
        // cleanup pass, 2026-07-18) - Helpers/SharedParamUtils.cs keeps only the genuinely generic
        // members shared with Purge and Duct Standards.
        // ─────────────────────────────────────────────────────────────────

        private sealed class FamilyTypeValueSnapshot
        {
            public FamilyTypeValueSnapshot(string familyTypeName, int familyTypeIndex, StorageType storageType)
            {
                FamilyTypeName = familyTypeName ?? string.Empty;
                FamilyTypeIndex = familyTypeIndex;
                StorageType = storageType;
            }

            public string FamilyTypeName { get; }

            public int FamilyTypeIndex { get; }

            public StorageType StorageType { get; }

            public bool HasValue { get; set; }

            public double? DoubleValue { get; set; }

            public int? IntegerValue { get; set; }

            public string StringValue { get; set; }

            public ElementId ElementIdValue { get; set; }

            public string ValueString { get; set; }
        }

        private static IList<FamilyParameter> CollectSharedFamilyParameters(FamilyManager familyManager)
        {
            var parameters = new List<FamilyParameter>();
            if (familyManager == null || familyManager.Parameters == null)
            {
                return parameters;
            }

            FamilyParameterSetIterator iterator = familyManager.Parameters.ForwardIterator();
            iterator.Reset();
            while (iterator.MoveNext())
            {
                var parameter = iterator.Current as FamilyParameter;
                if (parameter == null || !parameter.IsShared || parameter.Definition == null)
                {
                    continue;
                }

                string name = parameter.Definition.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                parameters.Add(parameter);
            }

            return parameters;
        }

        private static FamilyParameter FindSharedParameter(FamilyManager familyManager, SharedParamToFamilyParamItem item)
        {
            if (familyManager == null || item == null)
            {
                return null;
            }

            foreach (var parameter in CollectSharedFamilyParameters(familyManager))
            {
                if (parameter.Id != null && item.ParameterId != null && parameter.Id.IntValue() == item.ParameterId.IntValue())
                {
                    return parameter;
                }

                Guid parameterGuid = SharedParamUtils.TryGetSharedGuid(parameter);
                if (item.SharedGuid != Guid.Empty && parameterGuid != Guid.Empty && parameterGuid == item.SharedGuid)
                {
                    return parameter;
                }

                string parameterName = parameter.Definition?.Name ?? string.Empty;
                if (parameterName.Equals(item.Name ?? string.Empty, StringComparison.CurrentCultureIgnoreCase))
                {
                    return parameter;
                }
            }

            return null;
        }

        private static IList<FamilyTypeValueSnapshot> CaptureValues(
            FamilyManager familyManager,
            FamilyParameter sourceParameter,
            IList<string> warnings)
        {
            var snapshots = new List<FamilyTypeValueSnapshot>();
            if (familyManager == null || sourceParameter == null)
            {
                return snapshots;
            }

            IList<FamilyType> familyTypes = SharedParamUtils.GetFamilyTypes(familyManager);
            if (familyTypes.Count == 0)
            {
                return snapshots;
            }

            FamilyType originalType = familyManager.CurrentType;

            try
            {
                for (int i = 0; i < familyTypes.Count; i++)
                {
                    var type = familyTypes[i];
                    if (type == null)
                    {
                        continue;
                    }

                    var snapshot = new FamilyTypeValueSnapshot(type.Name, i, sourceParameter.StorageType);
                    snapshots.Add(snapshot);

                    try
                    {
                        familyManager.CurrentType = type;
                        snapshot.HasValue = type.HasValue(sourceParameter);
                        if (!snapshot.HasValue)
                        {
                            continue;
                        }

                        snapshot.ValueString = SafeAsValueString(type, sourceParameter);

                        switch (sourceParameter.StorageType)
                        {
                            case StorageType.Double:
                                snapshot.DoubleValue = type.AsDouble(sourceParameter);
                                break;
                            case StorageType.Integer:
                                snapshot.IntegerValue = type.AsInteger(sourceParameter);
                                break;
                            case StorageType.String:
                                snapshot.StringValue = type.AsString(sourceParameter);
                                break;
                            case StorageType.ElementId:
                                snapshot.ElementIdValue = type.AsElementId(sourceParameter);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings?.Add($"Could not read value from family type '{type.Name}': {ex.Message}");
                    }
                }
            }
            finally
            {
                TryRestoreCurrentType(familyManager, originalType);
            }

            return snapshots;
        }

        private static void RestoreValues(
            FamilyManager familyManager,
            FamilyParameter targetParameter,
            IList<FamilyTypeValueSnapshot> snapshots,
            IList<string> warnings)
        {
            if (familyManager == null || targetParameter == null || snapshots == null || snapshots.Count == 0)
            {
                return;
            }

            if (targetParameter.IsDeterminedByFormula)
            {
                return;
            }

            IList<FamilyType> familyTypes = SharedParamUtils.GetFamilyTypes(familyManager);
            if (familyTypes.Count == 0)
            {
                return;
            }

            var typeMap = new Dictionary<string, FamilyType>(StringComparer.CurrentCultureIgnoreCase);
            for (int i = 0; i < familyTypes.Count; i++)
            {
                var type = familyTypes[i];
                if (type == null || string.IsNullOrWhiteSpace(type.Name))
                {
                    continue;
                }

                if (!typeMap.ContainsKey(type.Name))
                {
                    typeMap.Add(type.Name, type);
                }
            }

            FamilyType originalType = familyManager.CurrentType;

            try
            {
                for (int i = 0; i < snapshots.Count; i++)
                {
                    var snapshot = snapshots[i];
                    if (snapshot == null || !snapshot.HasValue)
                    {
                        continue;
                    }

                    FamilyType type = null;
                    if (!string.IsNullOrWhiteSpace(snapshot.FamilyTypeName))
                    {
                        typeMap.TryGetValue(snapshot.FamilyTypeName, out type);
                    }

                    if (type == null &&
                        snapshot.FamilyTypeIndex >= 0 &&
                        snapshot.FamilyTypeIndex < familyTypes.Count)
                    {
                        type = familyTypes[snapshot.FamilyTypeIndex];
                    }

                    if (type == null)
                    {
                        warnings?.Add($"Could not find the original family type when restoring values.");
                        continue;
                    }

                    try
                    {
                        familyManager.CurrentType = type;

                        switch (targetParameter.StorageType)
                        {
                            case StorageType.Double:
                                if (snapshot.DoubleValue.HasValue)
                                {
                                    familyManager.Set(targetParameter, snapshot.DoubleValue.Value);
                                }
                                else
                                {
                                    TrySetValueString(familyManager, targetParameter, snapshot.ValueString, warnings, snapshot.FamilyTypeName);
                                }
                                break;
                            case StorageType.Integer:
                                if (snapshot.IntegerValue.HasValue)
                                {
                                    familyManager.Set(targetParameter, snapshot.IntegerValue.Value);
                                }
                                else
                                {
                                    TrySetValueString(familyManager, targetParameter, snapshot.ValueString, warnings, snapshot.FamilyTypeName);
                                }
                                break;
                            case StorageType.String:
                                familyManager.Set(targetParameter, snapshot.StringValue ?? string.Empty);
                                break;
                            case StorageType.ElementId:
                                familyManager.Set(targetParameter, snapshot.ElementIdValue ?? ElementId.InvalidElementId);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings?.Add($"Could not restore value for type '{snapshot.FamilyTypeName}': {ex.Message}");
                    }
                }
            }
            finally
            {
                TryRestoreCurrentType(familyManager, originalType);
            }
        }

        private static bool TryRestoreFormula(
            FamilyManager familyManager,
            FamilyParameter parameter,
            string formula,
            IList<string> warnings)
        {
            if (familyManager == null || parameter == null || string.IsNullOrWhiteSpace(formula))
            {
                return true;
            }

            if (!parameter.CanAssignFormula)
            {
                warnings?.Add("Formula could not be restored because this parameter does not support formulas.");
                return false;
            }

            try
            {
                familyManager.SetFormula(parameter, formula);
                return true;
            }
            catch (Exception ex)
            {
                warnings?.Add($"Formula could not be restored: {ex.Message}");
                return false;
            }
        }

        private static void TryRestoreReportingState(
            FamilyManager familyManager,
            FamilyParameter parameter,
            bool wasReporting,
            IList<string> warnings)
        {
            if (!wasReporting || familyManager == null || parameter == null)
            {
                return;
            }

            if (parameter.IsReporting)
            {
                return;
            }

            try
            {
                familyManager.MakeReporting(parameter);
            }
            catch (Exception ex)
            {
                warnings?.Add($"Reporting state could not be restored: {ex.Message}");
            }
        }

        private static string GetFormula(FamilyParameter parameter)
        {
            if (parameter == null)
            {
                return string.Empty;
            }

            try
            {
                return parameter.Formula ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SafeAsValueString(FamilyType familyType, FamilyParameter parameter)
        {
            try
            {
                return familyType.AsValueString(parameter);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void TrySetValueString(
            FamilyManager familyManager,
            FamilyParameter parameter,
            string valueString,
            IList<string> warnings,
            string familyTypeName)
        {
            if (familyManager == null || parameter == null || string.IsNullOrWhiteSpace(valueString))
            {
                return;
            }

            try
            {
                familyManager.SetValueString(parameter, valueString);
            }
            catch (Exception ex)
            {
                warnings?.Add($"Could not restore value string for type '{familyTypeName}': {ex.Message}");
            }
        }

        private static void TryRestoreCurrentType(FamilyManager familyManager, FamilyType originalType)
        {
            if (familyManager == null || originalType == null)
            {
                return;
            }

            try
            {
                familyManager.CurrentType = originalType;
            }
            catch
            {
                // Safe to ignore; failing to restore current type should not crash the command.
            }
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
