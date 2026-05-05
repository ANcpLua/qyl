
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class MetricDataPoint
    {
        public DateTimeOffset Timestamp { get; set; }

        [JsonPropertyName("value")]
        public double ValueName { get; set; }


    }
}
