
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Session
{

    public partial class SessionEntity
    {
        [StringConstraint(MinLength = 1, MaxLength = 128)]
        [JsonPropertyName("session.id")]
        public string SessionId { get; set; }

        [StringConstraint(MinLength = 1, MaxLength = 256)]
        [JsonPropertyName("user.id")]
        public string UserId { get; set; }

        [JsonPropertyName("start_time")]
        public DateTimeOffset StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public DateTimeOffset? EndTime { get; set; }

        [JsonPropertyName("duration_ms")]
        public double? DurationMs { get; set; }

        [NumericConstraint<int>(MinValue = 0)]
        [JsonPropertyName("trace_count")]
        public int TraceCount { get; set; }

        [NumericConstraint<int>(MinValue = 0)]
        [JsonPropertyName("span_count")]
        public int SpanCount { get; set; }

        [NumericConstraint<int>(MinValue = 0)]
        [JsonPropertyName("error_count")]
        public int ErrorCount { get; set; }

        public string[] Services { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SessionState State { get; set; }

        public SessionClientInfo Client { get; set; }

        public SessionGeoInfo Geo { get; set; }

        [JsonPropertyName("genai_usage")]
        public SessionGenAiUsage GenaiUsage { get; set; }


    }
}
