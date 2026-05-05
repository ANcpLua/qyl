
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;

namespace Qyl.Api
{

    public partial class MetricMetadata
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string Unit { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MetricType TypeName { get; set; }

        [JsonPropertyName("label_keys")]
        public string[] LabelKeys { get; set; }

        public string[] Services { get; set; }


    }
}
