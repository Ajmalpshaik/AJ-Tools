using Newtonsoft.Json;

namespace AJTools.Models.DuctStandards
{
    public class MaterialInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "GI";

        [JsonProperty("density_kg_m3")]
        public double DensityKgM3 { get; set; } = 7850.0;
    }
}
