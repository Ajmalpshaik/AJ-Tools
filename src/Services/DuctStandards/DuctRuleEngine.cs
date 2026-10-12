using System;
using System.Collections.Generic;
using AJTools.Models.DuctStandards;

namespace AJTools.Services.DuctStandards
{
    internal static class DuctRuleEngine
    {
        /// <summary>
        /// Finds the matching rule for the given shape, pressure class, and governing size.
        /// Returns null if no rule matches.
        /// </summary>
        /// <remarks>
        /// Rule bands are whole millimetres (e.g. 0-400 then 401-550), but duct sizes come from Revit's
        /// feet x 304.8 and are rarely whole: 16" is 406.4 mm, 48" is 1219.2 mm, and even a metric
        /// 400 mm duct can read back as 400.0000001. Such sizes fall in the 1 mm gap between two bands.
        /// So the exact size is tried first (nothing that matched before changes), and only when no band
        /// holds it is it retried rounded to the nearest whole mm (0.5 rounds up, to the larger band).
        /// </remarks>
        public static DuctRule FindRule(List<DuctRule> rules, string shape, string pressureClass, double sizeMm)
        {
            DuctRule rule = FindRuleForSize(rules, shape, pressureClass, sizeMm);
            if (rule != null)
                return rule;

            double roundedMm = Math.Round(sizeMm, MidpointRounding.AwayFromZero);
            return roundedMm != sizeMm
                ? FindRuleForSize(rules, shape, pressureClass, roundedMm)
                : null;
        }

        private static DuctRule FindRuleForSize(List<DuctRule> rules, string shape, string pressureClass, double sizeMm)
        {
            if (rules == null || string.IsNullOrEmpty(shape) || string.IsNullOrEmpty(pressureClass))
                return null;

            string shapeLower = shape.ToLowerInvariant();
            string pressureLower = pressureClass.ToLowerInvariant();

            foreach (var rule in rules)
            {
                if (!string.Equals(rule.Shape, shapeLower, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(rule.Pressure, pressureLower, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (sizeMm >= rule.MinMm && sizeMm <= rule.MaxMm)
                    return rule;
            }

            return null;
        }

        /// <summary>
        /// Builds a human-readable rule source string for traceability.
        /// </summary>
        public static string BuildRuleSource(DuctStandardsConfig config, DuctRule rule, string shape, string pressureClass)
        {
            string standardName = config.General?.StandardName ?? "Rule";
            return string.Format("{0} | {1} | {2} | {3}-{4} mm",
                standardName, shape, pressureClass,
                rule.MinMm, rule.MaxMm);
        }

        /// <summary>
        /// Reads the pressure class from the duct parameter or returns the config default.
        /// inputValue is what the duct's own parameter holds (null when it has none), so the caller can
        /// tell "empty, default used" apart from "filled in, but not low/medium/high".
        /// </summary>
        public static string GetPressureClass(Autodesk.Revit.DB.Element duct, DuctStandardsConfig config, out string inputValue)
        {
            string paramName = config.ParameterMap?.PressureClass ?? "Duct Pressure Class";
            inputValue = ReadStringParam(duct, paramName);
            if (IsKnownPressureClass(inputValue))
                return inputValue.Trim().ToLowerInvariant();
            return (config.DefaultPressureClass ?? "low").ToLowerInvariant();
        }

        /// <summary>True for low / medium / high (any case, surrounding spaces ignored).</summary>
        public static bool IsKnownPressureClass(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string lower = value.Trim().ToLowerInvariant();
            return lower == "low" || lower == "medium" || lower == "high";
        }

        /// <summary>
        /// Reads the material name from the duct parameter or returns the config default.
        /// </summary>
        public static string GetMaterialName(Autodesk.Revit.DB.Element duct, DuctStandardsConfig config)
        {
            string paramName = config.ParameterMap?.MaterialName ?? "Duct Material Name";
            string value = ReadStringParam(duct, paramName);
            if (!string.IsNullOrEmpty(value))
                return value.Trim();
            return config.DefaultMaterial ?? "GI";
        }

        /// <summary>
        /// Finds the density for a given material name from the config.
        /// </summary>
        public static double GetMaterialDensity(DuctStandardsConfig config, string materialName)
        {
            if (config.Materials != null)
            {
                foreach (var mat in config.Materials)
                {
                    if (string.Equals(mat.Name, materialName, StringComparison.OrdinalIgnoreCase))
                        return mat.DensityKgM3;
                }
            }
            return 7850.0; // default GI
        }

        private static string ReadStringParam(Autodesk.Revit.DB.Element elem, string paramName)
        {
            try
            {
                var p = elem.LookupParameter(paramName);
                if (p != null && p.StorageType == Autodesk.Revit.DB.StorageType.String)
                    return p.AsString();
            }
            catch { }
            return null;
        }
    }
}
