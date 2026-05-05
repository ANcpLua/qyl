
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;
using Qyl.Common;
using Qyl.OTel.Resource;

namespace Qyl.OTel.Traces
{

    public partial class Span
    {
        [StringConstraint(MinLength = 16, MaxLength = 16, Pattern = "^[a-f0-9]{16}$")]
        [JsonPropertyName("span_id")]
        public string SpanId { get; set; }

        [StringConstraint(MinLength = 32, MaxLength = 32, Pattern = "^[a-f0-9]{32}$")]
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [StringConstraint(MinLength = 16, MaxLength = 16, Pattern = "^[a-f0-9]{16}$")]
        [JsonPropertyName("parent_span_id")]
        public string ParentSpanId { get; set; }

        [JsonPropertyName("trace_state")]
        public string TraceState { get; set; }

        [StringConstraint(MinLength = 1)]
        public string Name { get; set; }

        public SpanKind Kind { get; set; }

        [JsonPropertyName("start_time_unix_nano")]
        public long StartTimeUnixNano { get; set; }

        [JsonPropertyName("end_time_unix_nano")]
        public long EndTimeUnixNano { get; set; }

        public Attribute[] Attributes { get; set; }

        [JsonPropertyName("dropped_attributes_count")]
        public long? DroppedAttributesCount { get; set; }

        public SpanEvent[] Events { get; set; }

        [JsonPropertyName("dropped_events_count")]
        public long? DroppedEventsCount { get; set; }

        public SpanLink[] Links { get; set; }

        [JsonPropertyName("dropped_links_count")]
        public long? DroppedLinksCount { get; set; }

        public SpanStatus Status { get; set; }

        public int? Flags { get; set; }

        public Qyl.OTel.Resource.Resource Resource { get; set; }

        [JsonPropertyName("instrumentation_scope")]
        public InstrumentationScope InstrumentationScope { get; set; }


    }
}
