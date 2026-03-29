using Newtonsoft.Json;

namespace AJTools.Models.FloorPlanImport
{
    public class PointJsonData
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }
    }
}
