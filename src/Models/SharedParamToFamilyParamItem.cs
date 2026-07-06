// Tool Name: Shared Parameter to Family Parameter - Item
// Description: Represents a shared family parameter candidate for conversion.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-26
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using Autodesk.Revit.DB;
using AJTools.Utils;

#if REVIT2024_OR_GREATER
using ParameterDataTypeId = Autodesk.Revit.DB.ForgeTypeId;
using ParameterGroupId = Autodesk.Revit.DB.ForgeTypeId;
#else
using ParameterDataTypeId = Autodesk.Revit.DB.ParameterType;
using ParameterGroupId = Autodesk.Revit.DB.BuiltInParameterGroup;
#endif

namespace AJTools.Models
{
    internal sealed class SharedParamToFamilyParamItem
    {
        public SharedParamToFamilyParamItem(FamilyParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            var definition = parameter.Definition;
            if (definition == null)
            {
                throw new ArgumentException("Family parameter definition is null.", nameof(parameter));
            }

            ParameterId = parameter.Id;
            Name = definition.Name ?? string.Empty;
            ParameterGroup = SharedParamUtils.GetDefinitionGroupId(definition);
            ParameterType = SharedParamUtils.GetDefinitionDataType(definition);
            StorageType = parameter.StorageType;
            IsInstance = parameter.IsInstance;
            IsReporting = SharedParamUtils.IsReporting(parameter);
            SharedGuid = SharedParamUtils.TryGetSharedGuid(parameter);
            GroupLabel = SharedParamUtils.GetGroupLabel(ParameterGroup);
            TypeLabel = SharedParamUtils.GetParameterTypeLabel(ParameterType);
        }

        public ElementId ParameterId { get; }

        public Guid SharedGuid { get; }

        public string Name { get; }

        public ParameterGroupId ParameterGroup { get; }

        public ParameterDataTypeId ParameterType { get; }

        public StorageType StorageType { get; }

        public bool IsInstance { get; }

        public bool IsReporting { get; }

        public string GroupLabel { get; }

        public string TypeLabel { get; }

        public string BehaviorLabel => IsInstance ? "Instance" : "Type";

        public string Descriptor => $"{BehaviorLabel} | {TypeLabel} | {GroupLabel}";

        public override string ToString()
        {
            return Name;
        }
    }
}
