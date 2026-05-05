
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api
{

    public partial class AlertFiringAcknowledgement
    {
        [JsonPropertyName("acknowledged_by")]
        public string AcknowledgedBy { get; set; }


    }
}
