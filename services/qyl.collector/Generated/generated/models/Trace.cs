
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.OTel.Traces
{

    public partial class Trace
    {
        [StringConstraint(MinLength = 32, MaxLength = 32, Pattern = "^[a-f0-9]{32}$")]
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        public Span[] Spans { get; set; }

        [JsonPropertyName("root_span")]
        public Span RootSpan { get; set; }

        [JsonPropertyName("span_count")]
        public int SpanCount { get; set; }

        [JsonPropertyName("duration_ns")]
        public long DurationNs { get; set; }

        [JsonPropertyName("start_time")]
        public DateTimeOffset StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public DateTimeOffset EndTime { get; set; }

        public string[] Services { get; set; }

        [JsonPropertyName("has_error")]
        public bool HasError { get; set; }


    }
}
