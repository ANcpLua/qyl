
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Identity
{

    public partial class ServiceDependency
    {
        [JsonPropertyName("source_service")]
        public string SourceService { get; set; }

        [JsonPropertyName("target_service")]
        public string TargetService { get; set; }

        [JsonPropertyName("request_count")]
        public long RequestCount { get; set; }

        [JsonPropertyName("error_rate")]
        public double ErrorRate { get; set; }

        [JsonPropertyName("avg_latency_ms")]
        public double AvgLatencyMs { get; set; }


    }
}
