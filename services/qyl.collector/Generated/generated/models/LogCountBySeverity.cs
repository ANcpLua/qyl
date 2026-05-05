
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;

namespace Qyl.OTel.Logs
{

    public partial class LogCountBySeverity
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SeverityText Severity { get; set; }

        public long Count { get; set; }

        public double Percentage { get; set; }


    }
}
