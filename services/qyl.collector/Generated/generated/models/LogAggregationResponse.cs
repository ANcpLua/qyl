
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class LogAggregationResponse
    {
        public LogAggregationBucket[] Results { get; set; }

        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }


    }
}
