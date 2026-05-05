
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;

namespace Qyl.Domains.Observe.Log
{

    public partial class LogSeverityStats
    {
        public SeverityNumber Severity { get; set; }

        [JsonPropertyName("severity_text")]
        public string SeverityText { get; set; }

        public long Count { get; set; }

        public double Percentage { get; set; }


    }
}
