
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Common;

namespace Qyl.OTel.Traces
{

    public partial class SpanLink
    {
        [StringConstraint(MinLength = 32, MaxLength = 32, Pattern = "^[a-f0-9]{32}$")]
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [StringConstraint(MinLength = 16, MaxLength = 16, Pattern = "^[a-f0-9]{16}$")]
        [JsonPropertyName("span_id")]
        public string SpanId { get; set; }

        [JsonPropertyName("trace_state")]
        public string TraceState { get; set; }

        public Attribute[] Attributes { get; set; }

        [JsonPropertyName("dropped_attributes_count")]
        public long? DroppedAttributesCount { get; set; }

        public int? Flags { get; set; }


    }
}
