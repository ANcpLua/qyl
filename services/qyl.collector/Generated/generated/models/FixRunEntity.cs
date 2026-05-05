
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Alerting
{

    public partial class FixRunEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("issue_id")]
        public string IssueId { get; set; }

        [JsonPropertyName("alert_firing_id")]
        public string AlertFiringId { get; set; }

        [JsonPropertyName("trigger_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FixTriggerType TriggerType { get; set; }

        public string Strategy { get; set; }

        [JsonPropertyName("model_name")]
        public string ModelName { get; set; }

        [JsonPropertyName("model_provider")]
        public string ModelProvider { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FixRunStatus Status { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("tokens_used")]
        public int? TokensUsed { get; set; }

        [JsonPropertyName("duration_ms")]
        public int? DurationMs { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("started_at")]
        public DateTimeOffset? StartedAt { get; set; }

        [JsonPropertyName("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }


    }
}
