
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Error
{

    public partial class ErrorEntity
    {
        [JsonPropertyName("error_id")]
        public string ErrorId { get; set; }

        [JsonPropertyName("error.type")]
        public string ErrorType { get; set; }

        public string Message { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ErrorCategory Category { get; set; }

        public string Fingerprint { get; set; }

        [JsonPropertyName("first_seen")]
        public DateTimeOffset FirstSeen { get; set; }

        [JsonPropertyName("last_seen")]
        public DateTimeOffset LastSeen { get; set; }

        [JsonPropertyName("occurrence_count")]
        public long OccurrenceCount { get; set; }

        [JsonPropertyName("affected_users")]
        public long? AffectedUsers { get; set; }

        [JsonPropertyName("affected_services")]
        public string[] AffectedServices { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ErrorStatus Status { get; set; }

        [JsonPropertyName("assigned_to")]
        public string AssignedTo { get; set; }

        [JsonPropertyName("issue_url")]
        public string IssueUrl { get; set; }

        [JsonPropertyName("sample_traces")]
        public string[] SampleTraces { get; set; }


    }
}
