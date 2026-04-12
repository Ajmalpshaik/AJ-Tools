using Newtonsoft.Json;

namespace AJTools.Models.DuctStandards
{
    public class DuctRule
    {
        [JsonProperty("shape")]
        public string Shape { get; set; } = "rectangular";

        [JsonProperty("pressure")]
        public string Pressure { get; set; } = "low";

        [JsonProperty("min_mm")]
        public double MinMm { get; set; }

        [JsonProperty("max_mm")]
        public double MaxMm { get; set; }

        [JsonProperty("thickness_mm")]
        public double ThicknessMm { get; set; }

        [JsonProperty("gauge")]
        public string Gauge { get; set; } = "";

        [JsonProperty("reinforcement")]
        public bool Reinforcement { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; } = "";
    }
}
