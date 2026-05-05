
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class LogAggregationBucket
    {
        public string Key { get; set; }

        [JsonPropertyName("value")]
        public double ValueName { get; set; }

        public long Count { get; set; }

        public DateTimeOffset? Timestamp { get; set; }


    }
}
