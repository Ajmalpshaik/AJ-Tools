using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.DuctStandards;

namespace AJTools.Services.DuctStandards
{
    internal static class DuctStandardsProcessor
    {
        /// <summary>
        /// Processes a list of duct elements: calculates values and writes to parameters.
        /// Must be called within a Revit Transaction.
        /// </summary>
        public static DuctProcessingReport Process(List<Element> ducts, DuctStandardsConfig config, Document doc)
        {
            var report = new DuctProcessingReport
            {
                TotalDucts = ducts.Count
            };

            bool writeToRevit = config.General?.WriteToRevit ?? true;
            bool includeAllowances = config.General?.IncludeAllowances ?? true;

            foreach (var duct in ducts)
            {
                DuctCalculationResult result;
                try
                {
                    result = CalculateSingle(duct, config, doc, includeAllowances);
                }
                catch (Exception ex)
                {
                    result = new DuctCalculationResult
                    {
                        ElementId = duct.Id.IntegerValue,
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }

                report.Results.Add(result);

                if (!result.Success)
                {
                    if (string.IsNullOrEmpty(result.ErrorMessage))
                        report.Skipped++;
                    else
                        report.Failed++;
                    continue;
                }

                // Write back
                if (writeToRevit)
                {
                    try
                    {
                        var written = DuctParameterWriter.WriteResults(duct, config, result);
                        if (GetBool(written, "sheet_thickness")) report.ThicknessWritten++;
                        if (GetBool(written, "gauge")) report.GaugeWritten++;
                        if (GetBool(written, "weight_per_meter")) report.WeightPerMeterWritten++;
                        if (GetBool(written, "total_weight")) report.TotalWeightWritten++;
                        if (GetBool(written, "sheet_area")) report.SheetAreaWritten++;
                    }
                    catch
                    {
                        // Parameter write failure should not crash the loop
                    }
                }

                report.Processed++;
            }

            return report;
        }

        /// <summary>
        /// Calculates values for a single duct without writing to Revit.
        /// </summary>
        public static DuctCalculationResult CalculateSingle(Element duct, DuctStandardsConfig config, Document doc, bool includeAllowances)
        {
            var result = new DuctCalculationResult
            {
                ElementId = duct.Id.IntegerValue
            };

            // Shape
            string shape = DuctShapeService.GetShape(duct, doc);
            if (shape == null)
            {
                result.ErrorMessage = "Unsupported or undetectable shape";
                return result;
            }
            result.Shape = shape;

            // Pressure class and material
            result.PressureClass = DuctRuleEngine.GetPressureClass(duct, config);
            result.MaterialName = DuctRuleEngine.GetMaterialName(duct, config);
            double density = DuctRuleEngine.GetMaterialDensity(config, result.MaterialName);

            // Size
            double sizeMm = DuctSizeService.GetGoverningSize(duct, shape);
            if (sizeMm <= 0)
            {
                result.ErrorMessage = "Size not found";
                return result;
            }
            result.GoverningSize_mm = Math.Round(sizeMm, 2);

            // Dimensions for area calc
            result.Width_mm = DuctSizeService.GetWidthMm(duct);
            result.Height_mm = DuctSizeService.GetHeightMm(duct);
            result.Diameter_mm = DuctSizeService.GetDiameterMm(duct);

            // Rule
            var rule = DuctRuleEngine.FindRule(config.Rules, shape, result.PressureClass, sizeMm);
            if (rule == null)
            {
                result.ErrorMessage = string.Format("No rule for {0} / {1} / {2:F0} mm", shape, result.PressureClass, sizeMm);
                return result;
            }

            result.ThicknessMm = rule.ThicknessMm;
            result.Gauge = rule.Gauge;
            result.ReinforcementRequired = rule.Reinforcement;
            result.RuleSource = DuctRuleEngine.BuildRuleSource(config, rule, shape, result.PressureClass);

            // Length
            double lengthM = DuctSizeService.GetLengthM(duct);
            if (lengthM <= 0)
            {
                result.ErrorMessage = "Length not found";
                return result;
            }
            result.Length_m = Math.Round(lengthM, 3);

            // Area
            double areaM2 = 0.0;
            switch (shape)
            {
                case "rectangular":
                    if (result.Width_mm <= 0 || result.Height_mm <= 0)
                    {
                        result.ErrorMessage = "Rectangular dimensions not found";
                        return result;
                    }
                    areaM2 = DuctWeightService.CalcRectangularArea(result.Width_mm, result.Height_mm, lengthM);
                    break;

                case "round":
                    if (result.Diameter_mm <= 0)
                    {
                        result.ErrorMessage = "Round diameter not found";
                        return result;
                    }
                    areaM2 = DuctWeightService.CalcRoundArea(result.Diameter_mm, lengthM);
                    break;

                case "oval":
                    if (result.Width_mm <= 0 || result.Height_mm <= 0)
                    {
                        result.ErrorMessage = "Oval dimensions not found";
                        return result;
                    }
                    areaM2 = DuctWeightService.CalcOvalArea(result.Width_mm, result.Height_mm, lengthM);
                    break;
            }

            result.SheetArea_m2 = Math.Round(areaM2, 4);

            // Weight
            double baseWeight = DuctWeightService.CalcBaseWeight(areaM2, rule.ThicknessMm, density);
            result.BaseWeight_kg = Math.Round(baseWeight, 4);

            double totalWeight = DuctWeightService.CalcTotalWeight(
                baseWeight, rule.Reinforcement, config.Allowances, includeAllowances);
            result.TotalWeight_kg = Math.Round(totalWeight, 4);
            result.WeightPerMeter_kg = lengthM > 0 ? Math.Round(totalWeight / lengthM, 4) : 0.0;

            result.Success = true;
            return result;
        }

        private static bool GetBool(Dictionary<string, bool> dict, string key)
        {
            bool val;
            return dict.TryGetValue(key, out val) && val;
        }
    }
}
