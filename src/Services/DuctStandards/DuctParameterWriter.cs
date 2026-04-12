using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.DuctStandards;

namespace AJTools.Services.DuctStandards
{
    internal static class DuctParameterWriter
    {
        /// <summary>
        /// Writes all calculated values to the duct element parameters.
        /// Returns a dictionary of parameter key to success status.
        /// </summary>
        public static Dictionary<string, bool> WriteResults(Element duct, DuctStandardsConfig config, DuctCalculationResult result)
        {
            var map = config.ParameterMap ?? new DuctParameterMap();
            var written = new Dictionary<string, bool>();

            written["sheet_thickness"] = SetParam(duct, map.SheetThickness, result.ThicknessMm);
            written["gauge"] = SetParamString(duct, map.Gauge, result.Gauge);
            written["weight_per_meter"] = SetParam(duct, map.WeightPerMeter, result.WeightPerMeter_kg);
            written["total_weight"] = SetParam(duct, map.TotalWeight, result.TotalWeight_kg);
            written["sheet_area"] = SetParam(duct, map.SheetArea, result.SheetArea_m2);
            written["pressure_class"] = SetParamString(duct, map.PressureClass, result.PressureClass);
            written["material_name"] = SetParamString(duct, map.MaterialName, result.MaterialName);

            if (config.General != null && config.General.WriteRuleSource)
                written["rule_source"] = SetParamString(duct, map.RuleSource, result.RuleSource);

            return written;
        }

        private static bool SetParam(Element elem, string paramName, double value)
        {
            if (string.IsNullOrEmpty(paramName))
                return false;

            try
            {
                return SetOnElementAndType(elem, paramName, p => SetDoubleValue(p, value));
            }
            catch
            {
                return false;
            }
        }

        private static bool SetParamString(Element elem, string paramName, string value)
        {
            if (string.IsNullOrEmpty(paramName))
                return false;

            try
            {
                return SetOnElementAndType(elem, paramName, p => SetStringValue(p, value));
            }
            catch
            {
                return false;
            }
        }

        private static bool SetOnElementAndType(Element elem, string paramName, System.Func<Parameter, bool> setter)
        {
            bool wrote = false;

            foreach (Parameter p in elem.GetParameters(paramName))
            {
                if (setter(p))
                    wrote = true;
            }

            ElementId typeId = elem.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element type = elem.Document.GetElement(typeId);
                if (type != null)
                {
                    foreach (Parameter p in type.GetParameters(paramName))
                    {
                        if (setter(p))
                            wrote = true;
                    }
                }
            }

            return wrote;
        }

        private static bool SetDoubleValue(Parameter p, double value)
        {
            if (p == null || p.IsReadOnly)
                return false;

            switch (p.StorageType)
            {
                case StorageType.Double:
                    p.Set(value);
                    return true;

                case StorageType.Integer:
                    p.Set((int)System.Math.Round(value));
                    return true;

                case StorageType.String:
                    p.Set(value.ToString("F4"));
                    return true;

                default:
                    return false;
            }
        }

        private static bool SetStringValue(Parameter p, string value)
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
    }
}
