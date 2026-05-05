
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Log
{

    public partial class LogPattern
    {
        [JsonPropertyName("pattern_id")]
        public string PatternId { get; set; }

        public string Template { get; set; }

        public string Sample { get; set; }

        public long Count { get; set; }

        [JsonPropertyName("first_seen")]
        public DateTimeOffset FirstSeen { get; set; }

        [JsonPropertyName("last_seen")]
        public DateTimeOffset LastSeen { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LogPatternTrend Trend { get; set; }

        [JsonPropertyName("severity_distribution")]
        public LogSeverityStats[] SeverityDistribution { get; set; }


    }
}
