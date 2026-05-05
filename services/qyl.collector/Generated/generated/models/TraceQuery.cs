
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;
using System.Text.Json.Nodes;

namespace Qyl.Api
{

    public partial class TraceQuery
    {
        public string Query { get; set; }

        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; }

        [JsonPropertyName("operation_name")]
        public string OperationName { get; set; }

        [JsonPropertyName("min_duration_ms")]
        public long? MinDurationMs { get; set; }

        [JsonPropertyName("max_duration_ms")]
        public long? MaxDurationMs { get; set; }

        public SpanStatusCode? Status { get; set; }

        [JsonPropertyName("start_time")]
        public DateTimeOffset? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public DateTimeOffset? EndTime { get; set; }

        public JsonObject Tags { get; set; }

        [NumericConstraint<int>(MinValue = 1, MaxValue = 1000)]
        public int? Limit { get; set; } = 100;

        public string Cursor { get; set; }


    }
}
