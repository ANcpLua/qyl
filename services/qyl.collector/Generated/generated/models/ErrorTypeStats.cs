
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Error
{

    public partial class ErrorTypeStats
    {
        [JsonPropertyName("error_type")]
        public string ErrorType { get; set; }

        public long Count { get; set; }

        public double Percentage { get; set; }

        [JsonPropertyName("affected_users")]
        public long? AffectedUsers { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ErrorStatus Status { get; set; }


    }
}
