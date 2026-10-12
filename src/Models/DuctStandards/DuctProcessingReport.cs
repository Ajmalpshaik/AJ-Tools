using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AJTools.Models.DuctStandards
{
    public class DuctProcessingReport
    {
        public int TotalDucts { get; set; }
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public int ThicknessWritten { get; set; }
        public int GaugeWritten { get; set; }
        public int WeightPerMeterWritten { get; set; }
        public int TotalWeightWritten { get; set; }
        public int SheetAreaWritten { get; set; }
        public List<DuctCalculationResult> Results { get; set; } = new List<DuctCalculationResult>();

        // Each distinct warning is listed once with the number of ducts it affected - on a whole-project
        // run one line per duct would repeat the same sentence hundreds of times.
        private readonly List<string> _warningOrder = new List<string>();
        private readonly Dictionary<string, int> _warningCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Records one duct's warnings (each counted once per duct).</summary>
        public void AddWarnings(DuctCalculationResult result)
        {
            if (result == null)
                return;

            foreach (string warning in result.Warnings.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct(StringComparer.Ordinal))
            {
                int count;
                if (_warningCounts.TryGetValue(warning, out count))
                {
                    _warningCounts[warning] = count + 1;
                }
                else
                {
                    _warningCounts[warning] = 1;
                    _warningOrder.Add(warning);
                }
            }
        }

        public string ToSummaryText()
        {
            string text = string.Format(
                "Total Ducts Found: {0}\n" +
                "Processed: {1}\n" +
                "Skipped: {2}\n" +
                "Failed: {3}\n\n" +
                "Sheet Thickness Written: {4}\n" +
                "Gauge Written: {5}\n" +
                "Weight per Meter Written: {6}\n" +
                "Total Weight Written: {7}\n" +
                "Sheet Area Written: {8}",
                TotalDucts, Processed, Skipped, Failed,
                ThicknessWritten, GaugeWritten,
                WeightPerMeterWritten, TotalWeightWritten, SheetAreaWritten);

            if (_warningOrder.Count > 0)
            {
                text += "\n\nWarnings:";
                foreach (string warning in _warningOrder)
                {
                    int count = _warningCounts[warning];
                    text += "\n- " + warning + " (" + count + (count == 1 ? " duct)" : " ducts)");
                }
            }

            return text;
        }

        public string ToCsvText()
        {
            var lines = new List<string>
            {
                "ElementId,Shape,PressureClass,Material,Size_mm,Length_m,Thickness_mm,Gauge,Area_m2,Weight_kg_per_m,TotalWeight_kg,Reinforcement,RuleSource,Status,Error,Warning"
            };

            foreach (var r in Results)
            {
                // Invariant culture: a comma decimal separator (e.g. "406,40") would split the number
                // across two CSV columns. Every text field is escaped, since names come from the model.
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4:F2},{5:F3},{6:F2},{7},{8:F4},{9:F4},{10:F4},{11},{12},{13},{14},{15}",
                    r.ElementId, EscapeCsv(r.Shape), EscapeCsv(r.PressureClass), EscapeCsv(r.MaterialName),
                    r.GoverningSize_mm, r.Length_m, r.ThicknessMm, EscapeCsv(r.Gauge),
                    r.SheetArea_m2, r.WeightPerMeter_kg, r.TotalWeight_kg,
                    r.ReinforcementRequired ? "Yes" : "No",
                    EscapeCsv(r.RuleSource),
                    r.Success ? "OK" : "FAIL",
                    EscapeCsv(r.ErrorMessage),
                    EscapeCsv(string.Join("; ", r.Warnings))));
            }

            return string.Join("\n", lines);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
