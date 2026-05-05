
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Metrics;

namespace Qyl.Api.Streaming
{

    public partial class MetricStreamEvent
    {
        [JsonPropertyName("type")]
        public string TypeName { get; } = "metric";

        public Metric Data { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
