
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Error
{

    public partial class ErrorStats
    {
        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }

        [JsonPropertyName("unique_types")]
        public int UniqueTypes { get; set; }

        [JsonPropertyName("error_rate")]
        public double ErrorRate { get; set; }

        [JsonPropertyName("by_category")]
        public ErrorCategoryStats[] ByCategory { get; set; }

        [JsonPropertyName("by_service")]
        public ErrorServiceStats[] ByService { get; set; }

        [JsonPropertyName("top_errors")]
        public ErrorTypeStats[] TopErrors { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ErrorTrend Trend { get; set; }


    }
}
