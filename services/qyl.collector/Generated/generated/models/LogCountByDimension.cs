
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.OTel.Logs
{

    public partial class LogCountByDimension
    {
        public string Dimension { get; set; }

        public long Count { get; set; }

        [JsonPropertyName("error_count")]
        public long ErrorCount { get; set; }


    }
}
