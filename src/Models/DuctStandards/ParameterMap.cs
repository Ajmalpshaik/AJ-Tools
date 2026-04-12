using Newtonsoft.Json;

namespace AJTools.Models.DuctStandards
{
    public class DuctParameterMap
    {
        [JsonProperty("sheet_thickness")]
        public string SheetThickness { get; set; } = "Sheet Thickness";

        [JsonProperty("gauge")]
        public string Gauge { get; set; } = "Gauge";

        [JsonProperty("weight_per_meter")]
        public string WeightPerMeter { get; set; } = "Duct Weight per Meter";

        [JsonProperty("total_weight")]
        public string TotalWeight { get; set; } = "Duct Total Weight";

        [JsonProperty("sheet_area")]
        public string SheetArea { get; set; } = "Duct Sheet Area";

        [JsonProperty("rule_source")]
        public string RuleSource { get; set; } = "Duct Rule Source";

        [JsonProperty("pressure_class")]
        public string PressureClass { get; set; } = "Duct Pressure Class";

        [JsonProperty("material_name")]
        public string MaterialName { get; set; } = "Duct Material Name";
    }
}
