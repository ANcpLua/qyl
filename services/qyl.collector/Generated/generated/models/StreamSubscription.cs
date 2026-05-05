
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using System.Text.Json.Nodes;

namespace Qyl.Api.Streaming
{

    public partial class StreamSubscription
    {
        [JsonPropertyName("event_types")]
        public StreamEventType[] EventTypes { get; set; }

        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; }

        [StringConstraint(MinLength = 32, MaxLength = 32, Pattern = "^[a-f0-9]{32}$")]
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [NumericConstraint<int>(MinValue = 1, MaxValue = 24)]
        [JsonPropertyName("min_severity")]
        public int? MinSeverity { get; set; }

        public JsonObject Filters { get; set; }

        [NumericConstraint<double>(MinValue = 0, MaxValue = 1)]
        [JsonPropertyName("sample_rate")]
        public double? SampleRate { get; set; }


    }
}
