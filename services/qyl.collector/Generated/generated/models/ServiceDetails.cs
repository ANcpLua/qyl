
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Common;

namespace Qyl.Api
{

    public partial class ServiceDetails
    {
        public string Name { get; set; }

        [JsonPropertyName("namespace_name")]
        public string NamespaceName { get; set; }

        [StringConstraint(Pattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
        public string Version { get; set; }

        [JsonPropertyName("instance_count")]
        public int InstanceCount { get; set; }

        [JsonPropertyName("last_seen")]
        public DateTimeOffset LastSeen { get; set; }

        [JsonPropertyName("resource_attributes")]
        public Attribute[] ResourceAttributes { get; set; }

        [JsonPropertyName("instrumentation_libraries")]
        public InstrumentationScope[] InstrumentationLibraries { get; set; }

        [JsonPropertyName("request_rate")]
        public double RequestRate { get; set; }

        [JsonPropertyName("error_rate")]
        public double ErrorRate { get; set; }

        [JsonPropertyName("avg_latency_ms")]
        public double AvgLatencyMs { get; set; }

        [JsonPropertyName("p99_latency_ms")]
        public double P99LatencyMs { get; set; }


    }
}
