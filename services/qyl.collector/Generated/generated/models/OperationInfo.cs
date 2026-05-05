
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;

namespace Qyl.Api
{

    public partial class OperationInfo
    {
        public string Name { get; set; }

        [JsonPropertyName("span_kind")]
        public SpanKind SpanKind { get; set; }

        [JsonPropertyName("request_count")]
        public long RequestCount { get; set; }

        [JsonPropertyName("error_count")]
        public long ErrorCount { get; set; }

        [JsonPropertyName("avg_duration_ms")]
        public double AvgDurationMs { get; set; }

        [JsonPropertyName("p99_duration_ms")]
        public double P99DurationMs { get; set; }


    }
}
