using Newtonsoft.Json;

namespace AJTools.Models.DuctStandards
{
    public class AllowanceSettings
    {
        [JsonProperty("seam_percent")]
        public double SeamPercent { get; set; } = 3.0;

        [JsonProperty("joint_percent")]
        public double JointPercent { get; set; } = 2.0;

        [JsonProperty("flange_percent")]
        public double FlangePercent { get; set; } = 4.0;

        [JsonProperty("reinforcement_percent")]
        public double ReinforcementPercent { get; set; } = 5.0;

        [JsonProperty("fittings_percent")]
        public double FittingsPercent { get; set; } = 10.0;

        [JsonProperty("wastage_percent")]
        public double WastagePercent { get; set; } = 5.0;
    }
}
