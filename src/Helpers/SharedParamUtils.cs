// Tool Name: Shared Parameter to Family Parameter - Utilities
// Description: Utility helpers for collecting, snapshotting, and restoring family parameter data.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-26
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.Utils
{
    internal static class SharedParamUtils
    {
        internal sealed class FamilyTypeValueSnapshot
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

        public static IList<FamilyParameter> GetSharedParameters(FamilyManager familyManager)
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

        public static FamilyParameter FindSharedParameter(FamilyManager familyManager, SharedParamToFamilyParamItem item)
        {
            if (familyManager == null || item == null)
            {
                return null;
            }

            foreach (var parameter in GetSharedParameters(familyManager))
            {
                if (parameter.Id != null && item.ParameterId != null && parameter.Id.IntegerValue == item.ParameterId.IntegerValue)
                {
                    return parameter;
                }

                Guid parameterGuid = TryGetSharedGuid(parameter);
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

        public static IList<FamilyTypeValueSnapshot> CaptureValues(
            FamilyManager familyManager,
            FamilyParameter sourceParameter,
            IList<string> warnings)
        {
            var snapshots = new List<FamilyTypeValueSnapshot>();
            if (familyManager == null || sourceParameter == null)
            {
                return snapshots;
            }

            IList<FamilyType> familyTypes = GetFamilyTypes(familyManager);
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

        public static void RestoreValues(
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

            IList<FamilyType> familyTypes = GetFamilyTypes(familyManager);
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

        public static bool TryRestoreFormula(
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

        public static void TryRestoreReportingState(
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

        public static string GetFormula(FamilyParameter parameter)
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

        public static Guid TryGetSharedGuid(FamilyParameter parameter)
        {
            if (parameter == null || !parameter.IsShared)
            {
                return Guid.Empty;
            }

            try
            {
                return parameter.GUID;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        public static bool IsReporting(FamilyParameter parameter)
        {
            if (parameter == null)
            {
                return false;
            }

            try
            {
                return parameter.IsReporting;
            }
            catch
            {
                return false;
            }
        }

        public static string GetGroupLabel(BuiltInParameterGroup group)
        {
            try
            {
                string label = LabelUtils.GetLabelFor(group);
                return string.IsNullOrWhiteSpace(label) ? group.ToString() : label;
            }
            catch
            {
                return group.ToString();
            }
        }

        public static string GetParameterTypeLabel(ParameterType parameterType)
        {
            try
            {
                string label = LabelUtils.GetLabelFor(parameterType);
                return string.IsNullOrWhiteSpace(label) ? parameterType.ToString() : label;
            }
            catch
            {
                return parameterType.ToString();
            }
        }

        public static IList<FamilyType> GetFamilyTypes(FamilyManager familyManager)
        {
            var types = new List<FamilyType>();
            if (familyManager == null || familyManager.Types == null)
            {
                return types;
            }

            FamilyTypeSetIterator iterator = familyManager.Types.ForwardIterator();
            iterator.Reset();
            while (iterator.MoveNext())
            {
                var type = iterator.Current as FamilyType;
                if (type != null)
                {
                    types.Add(type);
                }
            }

            return types;
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
}
