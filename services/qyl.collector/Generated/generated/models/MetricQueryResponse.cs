
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class MetricQueryResponse
    {
        [JsonPropertyName("metric_name")]
        public string MetricName { get; set; }

        public MetricTimeSeries[] Series { get; set; }


    }
}
