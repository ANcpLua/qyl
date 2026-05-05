
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using System.Text.Json.Nodes;
using Qyl.Common.Pagination;
using Qyl.OTel.Metrics;

namespace Qyl.Api
{

    public partial class MetricQueryRequest
    {
        [JsonPropertyName("metric_name")]
        public string MetricName { get; set; }

        public JsonObject Filters { get; set; }

        [JsonPropertyName("start_time")]
        public DateTimeOffset StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public DateTimeOffset EndTime { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TimeBucket? Step { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AggregationFunction? Aggregation { get; set; }

        [JsonPropertyName("group_by")]
        public string[] GroupBy { get; set; }


    }
}
