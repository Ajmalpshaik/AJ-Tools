using System.Collections.Generic;
using Newtonsoft.Json;

namespace AJTools.Models.DuctStandards
{
    public class DuctStandardsConfig
    {
        [JsonProperty("general")]
        public GeneralSettings General { get; set; } = new GeneralSettings();

        [JsonProperty("materials")]
        public List<MaterialInfo> Materials { get; set; } = new List<MaterialInfo>();

        [JsonProperty("default_material")]
        public string DefaultMaterial { get; set; } = "GI";

        [JsonProperty("pressure_classes")]
        public List<string> PressureClasses { get; set; } = new List<string> { "low", "medium", "high" };

        [JsonProperty("default_pressure_class")]
        public string DefaultPressureClass { get; set; } = "low";

        [JsonProperty("allowances")]
        public AllowanceSettings Allowances { get; set; } = new AllowanceSettings();

        [JsonProperty("rules")]
        public List<DuctRule> Rules { get; set; } = new List<DuctRule>();

        [JsonProperty("parameter_map")]
        public DuctParameterMap ParameterMap { get; set; } = new DuctParameterMap();
    }
}
