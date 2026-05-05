
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Error
{

    public partial class ErrorServiceStats
    {
        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; }

        public long Count { get; set; }

        [JsonPropertyName("error_rate")]
        public double ErrorRate { get; set; }

        [JsonPropertyName("top_error_type")]
        public string TopErrorType { get; set; }


    }
}
