using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.Purge;
using AJTools.Utils;

namespace AJTools.Services.Purge
{
    internal sealed class FamilyParameterScanService
    {
        private readonly Document _doc;
        private readonly FamilyManager _familyManager;
        private readonly FamilyParameterUsageEvaluator _usageEvaluator;

        private bool _dimensionScanFailed;
        private bool _probeSkippedReadOnly;
        private bool _valueScanErrors;

        public FamilyParameterScanService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _familyManager = _doc.FamilyManager;
            _usageEvaluator = new FamilyParameterUsageEvaluator(_doc, _familyManager);
        }

        public FamilyParameterPurgeScanResult Scan()
        {
            var rows = new List<FamilyParameterPurgeItem>();
            IList<FamilyParameter> parameters = GetAllFamilyParameters();

            Dictionary<int, HashSet<string>> formulaReferenceMap =
                _usageEvaluator.BuildFormulaReferenceMap(parameters);

            Dictionary<int, int> dimensionLabels = BuildDimensionLabelMapSafe();
            IList<FamilyType> familyTypes = SharedParamUtils.GetFamilyTypes(_familyManager);

            foreach (FamilyParameter parameter in parameters)
            {
                if (parameter == null || parameter.Definition == null)
                {
                    continue;
                }

                FamilyParameterPurgeItem row = BuildRow(
                    parameter,
                    formulaReferenceMap,
                    dimensionLabels,
                    familyTypes);

                rows.Add(row);
            }

            rows = rows
                .OrderBy(r => r.Status)
                .ThenBy(r => r.ParameterName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new FamilyParameterPurgeScanResult(rows, BuildLimitations());
        }

        private FamilyParameterPurgeItem BuildRow(
            FamilyParameter parameter,
            Dictionary<int, HashSet<string>> formulaReferenceMap,
            Dictionary<int, int> dimensionLabels,
            IList<FamilyType> familyTypes)
        {
            int parameterId = GetParameterIdValue(parameter);
            string formula = SafeGetFormula(parameter);
            bool hasFormula = !string.IsNullOrWhiteSpace(formula);

            var referencedBy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> owners;
            if (formulaReferenceMap.TryGetValue(parameterId, out owners))
            {
                foreach (string ownerName in owners)
                {
                    referencedBy.Add(ownerName);
                }
            }

            int dimensionCount = 0;
            dimensionLabels.TryGetValue(parameterId, out dimensionCount);

            AssociatedParameterUsage associatedUsage = _usageEvaluator.EvaluateAssociatedUsage(parameter);
            ValueUsageSummary valueUsage = _usageEvaluator.EvaluateValueUsage(parameter, familyTypes);

            if (valueUsage.ScanErrors > 0)
            {
                _valueScanErrors = true;
            }

            string deleteProbeReason;
            bool deletable = ProbeDeletion(parameter, out deleteProbeReason);

            var inUseReasons = new List<string>();
            var warnings = new List<string>();

            if (hasFormula)
            {
                inUseReasons.Add("Has formula.");
            }

            if (referencedBy.Count > 0)
            {
                inUseReasons.Add("Referenced by other formulas.");
            }

            if (SafeIsReporting(parameter))
            {
                inUseReasons.Add("Reporting parameter.");
            }

            if (dimensionCount > 0)
            {
                inUseReasons.Add("Used as dimension label.");
            }

            if (associatedUsage.AssociatedWithNestedFamilyParameters)
            {
                inUseReasons.Add("Associated with nested family parameters.");
            }

            if (associatedUsage.AssociatedWithVisibilityControls)
            {
                inUseReasons.Add("Associated with visibility control.");
            }

            if (associatedUsage.AssociatedElementParameterCount > 0)
            {
                inUseReasons.Add("Associated with element parameters (drives family behavior).");
            }

            if (valueUsage.HasMeaningfulValue)
            {
                inUseReasons.Add("Contains meaningful values across family types.");
            }
            else if (valueUsage.HasDefaultLikeOnlyValue)
            {
                warnings.Add("Only default-like values were detected.");
            }

            if (valueUsage.ScanErrors > 0)
            {
                warnings.Add("Some family types could not be inspected.");
            }

            ParameterPurgeStatus status;
            string reason;
            if (!deletable)
            {
                status = ParameterPurgeStatus.CannotDelete;
                reason = string.IsNullOrWhiteSpace(deleteProbeReason)
                    ? "Revit does not allow deleting this parameter."
                    : deleteProbeReason;
            }
            else if (inUseReasons.Count > 0)
            {
                status = ParameterPurgeStatus.InUse;
                reason = string.Join(" ", inUseReasons);
            }
            else if (warnings.Count > 0)
            {
                status = ParameterPurgeStatus.PossiblyUnused;
                reason = "No direct dependency found, but certainty is limited.";
            }
            else
            {
                status = ParameterPurgeStatus.SafeToPurge;
                reason = "No formula, dependency, association, or meaningful values detected.";
            }

            return new FamilyParameterPurgeItem
            {
                ParameterIdValue = parameterId,
                ParameterName = parameter.Definition.Name ?? string.Empty,
                ParameterTypeText = SharedParamUtils.GetParameterTypeLabel(RevitCompat.GetDataType(parameter.Definition)),
                ParameterGroupText = SharedParamUtils.GetGroupLabel(RevitCompat.GetGroup(parameter.Definition)),
                StorageTypeText = parameter.StorageType.ToString(),
                IsInstance = parameter.IsInstance,
                IsShared = parameter.IsShared,
                SharedGuid = parameter.IsShared ? SafeGetGuid(parameter) : Guid.Empty,
                HasFormula = hasFormula,
                FormulaText = string.IsNullOrWhiteSpace(formula) ? "None" : formula,
                IsReporting = SafeIsReporting(parameter),
                ReferencedByOtherFormulas = referencedBy.Count > 0,
                ReferencedByFormulaNames = referencedBy.Count > 0
                    ? string.Join(", ", referencedBy.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    : "None",
                AssociatedWithDimensions = dimensionCount > 0,
                AssociatedDimensionCount = dimensionCount,
                AssociatedWithNestedFamilyParameters = associatedUsage.AssociatedWithNestedFamilyParameters,
                AssociatedNestedParameterCount = associatedUsage.AssociatedNestedParameterCount,
                AssociatedElementParameterCount = associatedUsage.AssociatedElementParameterCount,
                AssociatedWithVisibilityControls = associatedUsage.AssociatedWithVisibilityControls,
                HasAnyValue = valueUsage.HasAnyValue,
                HasMeaningfulValue = valueUsage.HasMeaningfulValue,
                HasDefaultLikeOnlyValue = valueUsage.HasDefaultLikeOnlyValue,
                IsDeletable = deletable,
                DeleteProbeReason = string.IsNullOrWhiteSpace(deleteProbeReason) ? "Can be deleted." : deleteProbeReason,
                Status = status,
                Reason = reason,
                Warning = warnings.Count > 0 ? string.Join(" ", warnings) : string.Empty,
                DetailedNotes = BuildDetailedNotes(
                    parameter,
                    formula,
                    referencedBy,
                    dimensionCount,
                    associatedUsage,
                    valueUsage,
                    deletable,
                    deleteProbeReason,
                    inUseReasons,
                    warnings)
            };
        }

        private string BuildDetailedNotes(
            FamilyParameter parameter,
            string formula,
            ISet<string> referencedBy,
            int dimensionCount,
            AssociatedParameterUsage associatedUsage,
            ValueUsageSummary valueUsage,
            bool deletable,
            string deleteProbeReason,
            IList<string> inUseReasons,
            IList<string> warnings)
        {
            var lines = new List<string>
            {
                "Group: " + SharedParamUtils.GetGroupLabel(RevitCompat.GetGroup(parameter.Definition)),
                "Parameter Type: " + SharedParamUtils.GetParameterTypeLabel(RevitCompat.GetDataType(parameter.Definition)),
                "Storage: " + parameter.StorageType,
                "Shared GUID: " + (parameter.IsShared ? SafeGetGuid(parameter).ToString() : "N/A"),
                "Formula: " + (string.IsNullOrWhiteSpace(formula) ? "None" : formula),
                "Referenced by formulas: " + (referencedBy.Count > 0
                    ? string.Join(", ", referencedBy.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    : "None"),
                "Dimension labels: " + dimensionCount,
                "Associated element parameters: " + associatedUsage.AssociatedElementParameterCount,
                "Nested family associations: " + associatedUsage.AssociatedNestedParameterCount,
                "Visibility association: " + (associatedUsage.AssociatedWithVisibilityControls ? "Yes" : "No"),
                "Has any value in family types: " + (valueUsage.HasAnyValue ? "Yes" : "No"),
                "Has meaningful value: " + (valueUsage.HasMeaningfulValue ? "Yes" : "No"),
                "Deletable by probe: " + (deletable ? "Yes" : "No"),
                "Delete probe note: " + (string.IsNullOrWhiteSpace(deleteProbeReason) ? "None" : deleteProbeReason)
            };

            if (inUseReasons.Count > 0)
            {
                lines.Add("In-use signals: " + string.Join(" ", inUseReasons));
            }

            if (warnings.Count > 0)
            {
                lines.Add("Warnings: " + string.Join(" ", warnings));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private Dictionary<int, int> BuildDimensionLabelMapSafe()
        {
            try
            {
                return _usageEvaluator.BuildDimensionLabelMap();
            }
            catch
            {
                _dimensionScanFailed = true;
                return new Dictionary<int, int>();
            }
        }

        private bool ProbeDeletion(FamilyParameter parameter, out string reason)
        {
            reason = string.Empty;

            if (_doc.IsReadOnly)
            {
                _probeSkippedReadOnly = true;
                reason = "Document is read-only.";
                return false;
            }

            if (parameter == null)
            {
                reason = "Parameter is invalid.";
                return false;
            }

            if (parameter.IsReadOnly)
            {
                reason = "Parameter is read-only.";
                return false;
            }

            using (var transaction = new Transaction(_doc, "Probe Family Parameter Delete"))
            {
                try
                {
                    transaction.Start();

                    FamilyParameter current = FindParameterById(parameter.Id);
                    if (current == null)
                    {
                        reason = "Parameter was not found in current family context.";
                        transaction.RollBack();
                        return false;
                    }

                    _familyManager.RemoveParameter(current);
                    transaction.RollBack();
                    return true;
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        transaction.RollBack();
                    }

                    reason = ex.Message;
                    return false;
                }
            }
        }

        private IList<FamilyParameter> GetAllFamilyParameters()
        {
            var parameters = _familyManager.GetParameters();
            return parameters
                .Where(p => p != null && p.Definition != null && !string.IsNullOrWhiteSpace(p.Definition.Name))
                .OrderBy(p => p.Definition.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private FamilyParameter FindParameterById(ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId)
            {
                return null;
            }

            foreach (FamilyParameter parameter in _familyManager.GetParameters())
            {
                if (parameter == null || parameter.Id == null || parameter.Id == ElementId.InvalidElementId)
                {
                    continue;
                }

                if (parameter.Id.IntValue() == id.IntValue())
                {
                    return parameter;
                }
            }

            return null;
        }

        private IList<string> BuildLimitations()
        {
            var notes = new List<string>
            {
                "Formula dependency detection is based on parameter-name matching in formula text.",
                "Some geometry/constraint usage is inferred from associated element parameters exposed by Revit API."
            };

            if (_dimensionScanFailed)
            {
                notes.Add("Dimension label analysis could not be fully completed.");
            }

            if (_probeSkippedReadOnly)
            {
                notes.Add("Delete probing was skipped because the family document is read-only.");
            }

            if (_valueScanErrors)
            {
                notes.Add("Some family type values could not be read.");
            }

            return notes;
        }

        private static string SafeGetFormula(FamilyParameter parameter)
        {
            try
            {
                return parameter.Formula ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool SafeIsReporting(FamilyParameter parameter)
        {
            try
            {
                return parameter.IsReporting;
            }
            catch
            {
                return false;
            }
        }

        private static Guid SafeGetGuid(FamilyParameter parameter)
        {
            try
            {
                return parameter.GUID;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        private static int GetParameterIdValue(FamilyParameter parameter)
        {
            if (parameter == null || parameter.Id == null || parameter.Id == ElementId.InvalidElementId)
            {
                return int.MinValue;
            }

            return parameter.Id.IntValue();
        }
    }
}
