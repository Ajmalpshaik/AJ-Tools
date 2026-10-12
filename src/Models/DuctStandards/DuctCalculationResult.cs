using System.Collections.Generic;

namespace AJTools.Models.DuctStandards
{
    public class DuctCalculationResult
    {
        public int ElementId { get; set; }
        public string Shape { get; set; }
        public string PressureClass { get; set; }

        /// <summary>
        /// True when the duct's own pressure class parameter was empty and the config default was used.
        /// Only then may the writer fill the parameter in - a value the user entered is never overwritten.
        /// </summary>
        public bool PressureClassDefaulted { get; set; }

        public string MaterialName { get; set; }
        public double GoverningSize_mm { get; set; }
        public double Width_mm { get; set; }
        public double Height_mm { get; set; }
        public double Diameter_mm { get; set; }
        public double Length_m { get; set; }
        public double ThicknessMm { get; set; }
        public string Gauge { get; set; }
        public double SheetArea_m2 { get; set; }
        public double BaseWeight_kg { get; set; }
        public double WeightPerMeter_kg { get; set; }
        public double TotalWeight_kg { get; set; }
        public bool ReinforcementRequired { get; set; }
        public string RuleSource { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Plain-language notes for this duct that did not stop it being processed (an unrecognised
        /// pressure class, a parameter that could not be written, ...). Collected into the report.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();
    }
}
