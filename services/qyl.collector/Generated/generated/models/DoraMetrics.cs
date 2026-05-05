
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class DoraMetrics
    {
        [JsonPropertyName("deployment_frequency")]
        public double DeploymentFrequency { get; set; }

        [JsonPropertyName("lead_time_hours")]
        public double LeadTimeHours { get; set; }

        [JsonPropertyName("change_failure_rate")]
        public double ChangeFailureRate { get; set; }

        [JsonPropertyName("mttr_hours")]
        public double MttrHours { get; set; }

        [JsonPropertyName("performance_level")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DoraPerformanceLevel PerformanceLevel { get; set; }


    }
}
