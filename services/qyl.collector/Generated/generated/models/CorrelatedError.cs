
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Error
{

    public partial class CorrelatedError
    {
        [JsonPropertyName("error_id")]
        public string ErrorId { get; set; }

        [JsonPropertyName("error_type")]
        public string ErrorType { get; set; }

        [JsonPropertyName("correlation_strength")]
        public double CorrelationStrength { get; set; }

        [JsonPropertyName("temporal_relationship")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TemporalRelationship TemporalRelationship { get; set; }


    }
}
