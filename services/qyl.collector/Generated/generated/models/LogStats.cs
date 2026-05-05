
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.OTel.Logs
{

    public partial class LogStats
    {
        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }

        [JsonPropertyName("by_severity")]
        public LogCountBySeverity[] BySeverity { get; set; }

        [JsonPropertyName("by_service")]
        public LogCountByDimension[] ByService { get; set; }

        [JsonPropertyName("logs_per_second")]
        public double LogsPerSecond { get; set; }

        [JsonPropertyName("error_rate")]
        public double ErrorRate { get; set; }


    }
}
