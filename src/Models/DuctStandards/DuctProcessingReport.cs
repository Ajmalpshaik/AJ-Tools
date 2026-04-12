using System.Collections.Generic;

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

        public string ToSummaryText()
        {
            return string.Format(
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
        }

        public string ToCsvText()
        {
            var lines = new List<string>
            {
                "ElementId,Shape,PressureClass,Material,Size_mm,Length_m,Thickness_mm,Gauge,Area_m2,Weight_kg_per_m,TotalWeight_kg,Reinforcement,RuleSource,Status,Error"
            };

            foreach (var r in Results)
            {
                lines.Add(string.Format(
                    "{0},{1},{2},{3},{4:F2},{5:F3},{6:F2},{7},{8:F4},{9:F4},{10:F4},{11},{12},{13},{14}",
                    r.ElementId, r.Shape, r.PressureClass, r.MaterialName,
                    r.GoverningSize_mm, r.Length_m, r.ThicknessMm, r.Gauge,
                    r.SheetArea_m2, r.WeightPerMeter_kg, r.TotalWeight_kg,
                    r.ReinforcementRequired ? "Yes" : "No",
                    EscapeCsv(r.RuleSource ?? ""),
                    r.Success ? "OK" : "FAIL",
                    EscapeCsv(r.ErrorMessage ?? "")));
            }

            return string.Join("\n", lines);
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
