using Newtonsoft.Json;

namespace AJTools.Models.FloorPlanImport
{
    public class WallJsonData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("wallType")]
        public string WallType { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; }

        [JsonProperty("start")]
        public PointJsonData Start { get; set; }

        [JsonProperty("end")]
        public PointJsonData End { get; set; }

        [JsonProperty("height")]
        public double Height { get; set; }

        [JsonProperty("baseOffset")]
        public double? BaseOffset { get; set; }

        [JsonProperty("topOffset")]
        public double? TopOffset { get; set; }

        [JsonProperty("structural")]
        public bool? Structural { get; set; }

        [JsonProperty("roomBounding")]
        public bool? RoomBounding { get; set; }

        [JsonProperty("locationLine")]
        public string LocationLine { get; set; }

        [JsonProperty("comments")]
        public string Comments { get; set; }
    }
}
