
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;
using Qyl.Common;
using Qyl.OTel.Resource;

namespace Qyl.OTel.Logs
{

    public partial class LogRecord
    {
        [JsonPropertyName("time_unix_nano")]
        public long TimeUnixNano { get; set; }

        [JsonPropertyName("observed_time_unix_nano")]
        public long ObservedTimeUnixNano { get; set; }

        [JsonPropertyName("severity_number")]
        public SeverityNumber SeverityNumber { get; set; }

        [JsonPropertyName("severity_text")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SeverityText? SeverityText { get; set; }

        public object Body { get; set; }

        public Attribute[] Attributes { get; set; }

        [JsonPropertyName("dropped_attributes_count")]
        public long? DroppedAttributesCount { get; set; }

        public int? Flags { get; set; }

        [StringConstraint(MinLength = 32, MaxLength = 32, Pattern = "^[a-f0-9]{32}$")]
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [StringConstraint(MinLength = 16, MaxLength = 16, Pattern = "^[a-f0-9]{16}$")]
        [JsonPropertyName("span_id")]
        public string SpanId { get; set; }

        public Qyl.OTel.Resource.Resource Resource { get; set; }

        [JsonPropertyName("instrumentation_scope")]
        public InstrumentationScope InstrumentationScope { get; set; }


    }
}
