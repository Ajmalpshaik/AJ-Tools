using System.Collections.Generic;
using Newtonsoft.Json;

namespace AJTools.Models.FloorPlanImport
{
    public class FloorPlanJsonData
    {
        [JsonProperty("project")]
        public ProjectJsonData Project { get; set; }

        [JsonProperty("walls")]
        public List<WallJsonData> Walls { get; set; }
    }
}
