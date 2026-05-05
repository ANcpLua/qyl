
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Log
{

    public partial class LogAggregation
    {
        [JsonPropertyName("group_by")]
        public string[] GroupBy { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AggregationFunction Function { get; set; }

        [JsonPropertyName("field")]
        public string FieldName { get; set; }

        [JsonPropertyName("time_bucket")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TimeBucket? TimeBucket { get; set; }

        [JsonPropertyName("top_n")]
        public int? TopN { get; set; }


    }
}
