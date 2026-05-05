
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Session
{

    public partial class SessionStats
    {
        [JsonPropertyName("active_sessions")]
        public long ActiveSessions { get; set; }

        [JsonPropertyName("total_sessions")]
        public long TotalSessions { get; set; }

        [JsonPropertyName("unique_users")]
        public long UniqueUsers { get; set; }

        [JsonPropertyName("avg_duration_ms")]
        public double AvgDurationMs { get; set; }

        [JsonPropertyName("sessions_with_errors")]
        public long SessionsWithErrors { get; set; }

        [JsonPropertyName("sessions_with_genai")]
        public long SessionsWithGenAi { get; set; }

        [JsonPropertyName("bounce_rate")]
        public double BounceRate { get; set; }

        [JsonPropertyName("by_device_type")]
        public SessionDeviceStats[] ByDeviceType { get; set; }

        [JsonPropertyName("by_country")]
        public SessionCountryStats[] ByCountry { get; set; }


    }
}
