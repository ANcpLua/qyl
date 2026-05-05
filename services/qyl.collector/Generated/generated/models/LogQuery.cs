
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;

namespace Qyl.Domains.Observe.Log
{

    public partial class LogQuery
    {
        public string Query { get; set; }

        [JsonPropertyName("severity_min")]
        public SeverityNumber? SeverityMin { get; set; }

        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; }

        [StringConstraint(MinLength = 32, MaxLength = 32, Pattern = "^[a-f0-9]{32}$")]
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [StringConstraint(MinLength = 16, MaxLength = 16, Pattern = "^[a-f0-9]{16}$")]
        [JsonPropertyName("span_id")]
        public string SpanId { get; set; }

        [JsonPropertyName("time_start")]
        public DateTimeOffset? TimeStart { get; set; }

        [JsonPropertyName("time_end")]
        public DateTimeOffset? TimeEnd { get; set; }

        [JsonPropertyName("attribute_filters")]
        public AttributeFilter[] AttributeFilters { get; set; }

        [NumericConstraint<int>(MinValue = 1, MaxValue = 10000)]
        public int? Limit { get; set; }

        [JsonPropertyName("order_by")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LogOrderBy? OrderbyName { get; set; }


    }
}
