using Newtonsoft.Json;

namespace AJTools.Models.DuctStandards
{
    public class GeneralSettings
    {
        [JsonProperty("standard_name")]
        public string StandardName { get; set; } = "SMACNA Style Editable";

        [JsonProperty("edition")]
        public string Edition { get; set; } = "Project Editable";

        [JsonProperty("mode")]
        public string Mode { get; set; } = "editable";

        [JsonProperty("apply_scope_default")]
        public string ApplyScopeDefault { get; set; } = "project";

        [JsonProperty("write_to_revit")]
        public bool WriteToRevit { get; set; } = true;

        [JsonProperty("include_allowances")]
        public bool IncludeAllowances { get; set; } = true;

        [JsonProperty("write_rule_source")]
        public bool WriteRuleSource { get; set; } = true;

        [JsonProperty("skip_if_missing_required_parameter")]
        public bool SkipIfMissingRequiredParameter { get; set; }
    }
}
