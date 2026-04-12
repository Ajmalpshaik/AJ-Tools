namespace AJTools.Models.DuctStandards
{
    public class DuctCalculationResult
    {
        public int ElementId { get; set; }
        public string Shape { get; set; }
        public string PressureClass { get; set; }
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
    }
}
