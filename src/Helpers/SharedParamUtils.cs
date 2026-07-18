// Tool Name: Shared Parameter Utilities (generic)
// Description: Small reflection-safe FamilyParameter helpers shared across Purge, Duct Standards,
//              and the Shared Parameter to Family Parameter tool. The feature-specific snapshot/
//              restore algorithm used only by the Shared Param to Family Param conversion itself
//              (CaptureValues/RestoreValues/TryRestoreFormula/TryRestoreReportingState and friends)
//              was moved into SharedParamToFamilyParamService.cs - its only consumer - since that
//              logic isn't a generic reusable utility, unlike everything still in this file.
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2026-07-18
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

// Version-safe token types: legacy enums on Revit 2020-2021, ForgeTypeId on Revit 2022+.
// The version branch itself lives in RevitCompat; these aliases only pick the member type.
#if REVIT2022_OR_GREATER
using AjSpec = Autodesk.Revit.DB.ForgeTypeId;
using AjGroup = Autodesk.Revit.DB.ForgeTypeId;
#else
using AjSpec = Autodesk.Revit.DB.ParameterType;
using AjGroup = Autodesk.Revit.DB.BuiltInParameterGroup;
#endif

namespace AJTools.Utils
{
    internal static class SharedParamUtils
    {
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

        public static string GetGroupLabel(AjGroup group)
        {
            try
            {
                string label = RevitCompat.GroupLabel(group);
                return string.IsNullOrWhiteSpace(label) ? group.ToString() : label;
            }
            catch
            {
                return group.ToString();
            }
        }

        public static string GetParameterTypeLabel(AjSpec parameterType)
        {
            try
            {
                string label = RevitCompat.SpecLabel(parameterType);
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

    }
}
