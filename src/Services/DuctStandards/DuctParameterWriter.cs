using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.DuctStandards;
using AJTools.Utils;
#if REVIT2022_OR_GREATER
using AjSpec = Autodesk.Revit.DB.ForgeTypeId;
#else
using AjSpec = Autodesk.Revit.DB.ParameterType;
#endif

namespace AJTools.Services.DuctStandards
{
    internal static class DuctParameterWriter
    {
        /// <summary>
        /// Writes all calculated values to the duct element parameters.
        /// Returns a dictionary of parameter key to success status.
        /// A value skipped for a reason the user can fix (a type parameter, a parameter in a different
        /// unit) is added to result.Warnings so the report can say so.
        /// </summary>
        public static Dictionary<string, bool> WriteResults(Element duct, DuctStandardsConfig config, DuctCalculationResult result)
        {
            var map = config.ParameterMap ?? new DuctParameterMap();
            var written = new Dictionary<string, bool>();

            written["sheet_thickness"] = SetParam(duct, map.SheetThickness, result.ThicknessMm, true, result);
            written["gauge"] = SetParamString(duct, map.Gauge, result.Gauge, result);
            written["weight_per_meter"] = SetParam(duct, map.WeightPerMeter, result.WeightPerMeter_kg, false, result);
            written["total_weight"] = SetParam(duct, map.TotalWeight, result.TotalWeight_kg, false, result);
            written["sheet_area"] = SetParam(duct, map.SheetArea, result.SheetArea_m2, false, result);

            // The pressure class parameter is also the calculation's INPUT. It is only written to fill in
            // an empty value with the default that was used - a value the user entered is never replaced
            // (an unrecognised one used to be overwritten with the default, silently).
            if (result.PressureClassDefaulted)
                written["pressure_class"] = SetParamString(duct, map.PressureClass, result.PressureClass, result);

            written["material_name"] = SetParamString(duct, map.MaterialName, result.MaterialName, result);

            if (config.General != null && config.General.WriteRuleSource)
                written["rule_source"] = SetParamString(duct, map.RuleSource, result.RuleSource, result);

            return written;
        }

        private static bool SetParam(Element elem, string paramName, double value, bool valueIsMillimetres, DuctCalculationResult result)
        {
            if (string.IsNullOrEmpty(paramName))
                return false;

            try
            {
                return SetOnElement(elem, paramName, p => SetDoubleValue(p, paramName, value, valueIsMillimetres, result), result);
            }
            catch
            {
                return false;
            }
        }

        private static bool SetParamString(Element elem, string paramName, string value, DuctCalculationResult result)
        {
            if (string.IsNullOrEmpty(paramName))
                return false;

            try
            {
                return SetOnElement(elem, paramName, p => SetStringValue(p, paramName, value, result), result);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Writes the duct's own (instance) parameters only. A same-named TYPE parameter is shared by every
        /// duct of that type, so writing per-duct results into it left all of them showing whichever duct
        /// was processed last - when only a type parameter exists it is skipped and reported instead.
        /// </summary>
        private static bool SetOnElement(Element elem, string paramName, Func<Parameter, bool> setter, DuctCalculationResult result)
        {
            IList<Parameter> instanceParameters = elem.GetParameters(paramName);
            if (instanceParameters == null || instanceParameters.Count == 0)
            {
                if (HasTypeParameter(elem, paramName))
                {
                    AddWarning(result, string.Format(
                        "'{0}' was not written: it is a type parameter - each duct needs its own value, so make it an instance parameter",
                        paramName));
                }

                return false;
            }

            bool wrote = false;
            foreach (Parameter p in instanceParameters)
            {
                if (setter(p))
                    wrote = true;
            }

            return wrote;
        }

        private static bool HasTypeParameter(Element elem, string paramName)
        {
            ElementId typeId = elem.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
                return false;

            Element type = elem.Document.GetElement(typeId);
            if (type == null)
                return false;

            IList<Parameter> typeParameters = type.GetParameters(paramName);
            return typeParameters != null && typeParameters.Count > 0;
        }

        private static bool SetDoubleValue(Parameter p, string paramName, double value, bool valueIsMillimetres, DuctCalculationResult result)
        {
            if (p == null || p.IsReadOnly)
                return false;

            switch (p.StorageType)
            {
                case StorageType.Double:
                    // Revit keeps measured values in its own internal units (feet for a Length), so a raw
                    // mm / kg / m2 figure is only right in a plain Number parameter (what Create/Rebind
                    // Parameters makes). A millimetre value going into a Length parameter is converted
                    // with the house helper; any other unit-bearing parameter (Area, Mass, ...) is skipped
                    // and reported rather than written in the wrong unit.
                    if (IsSpec(p, RevitCompat.SpecNumber))
                    {
                        p.Set(value);
                        return true;
                    }

                    if (valueIsMillimetres && IsSpec(p, RevitCompat.SpecLength))
                    {
                        p.Set(RevitCompat.MmToInternal(value));
                        return true;
                    }

                    AddWarning(result, string.Format(
                        "'{0}' was not written: its parameter type is {1} - use a Number parameter{2}",
                        paramName, GetSpecLabel(p), valueIsMillimetres ? " or a Length parameter" : string.Empty));
                    return false;

                case StorageType.Integer:
                    p.Set((int)Math.Round(value));
                    return true;

                case StorageType.String:
                    p.Set(value.ToString("F4"));
                    return true;

                default:
                    return false;
            }
        }

        private static bool SetStringValue(Parameter p, string paramName, string value, DuctCalculationResult result)
        {
            if (p == null || p.IsReadOnly)
                return false;

            switch (p.StorageType)
            {
                case StorageType.String:
                    p.Set(value ?? "");
                    return true;

                case StorageType.Double:
                    double dVal;
                    if (double.TryParse(value, out dVal))
                    {
                        // A number held as text (a gauge of "26") is only right in a plain Number
                        // parameter - in a unit-bearing one Revit would read it as feet, square feet, ...
                        if (!IsSpec(p, RevitCompat.SpecNumber))
                        {
                            AddWarning(result, string.Format(
                                "'{0}' was not written: its parameter type is {1} - use a Text or Number parameter",
                                paramName, GetSpecLabel(p)));
                            return false;
                        }

                        p.Set(dVal);
                        return true;
                    }
                    return false;

                case StorageType.Integer:
                    int iVal;
                    if (int.TryParse(value, out iVal))
                    {
                        p.Set(iVal);
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>True when the parameter's data type is the given spec. Version-safe through RevitCompat
        /// (enum on 2020-2021; ForgeTypeId on 2022+, whose == ignores the version suffix).</summary>
        private static bool IsSpec(Parameter p, AjSpec spec)
        {
            try
            {
                return p.Definition != null && RevitCompat.GetDataType(p.Definition) == spec;
            }
            catch
            {
                return false;
            }
        }

        private static string GetSpecLabel(Parameter p)
        {
            try
            {
                return SharedParamUtils.GetParameterTypeLabel(RevitCompat.GetDataType(p.Definition));
            }
            catch
            {
                return "a unit-based type";
            }
        }

        private static void AddWarning(DuctCalculationResult result, string message)
        {
            if (result != null && !result.Warnings.Contains(message))
                result.Warnings.Add(message);
        }
    }
}
