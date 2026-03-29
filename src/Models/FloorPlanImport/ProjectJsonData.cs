using Newtonsoft.Json;

namespace AJTools.Models.FloorPlanImport
{
    public class ProjectJsonData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("units")]
        public string Units { get; set; }

        [JsonProperty("defaultLevel")]
        public string DefaultLevel { get; set; }
    }
}
