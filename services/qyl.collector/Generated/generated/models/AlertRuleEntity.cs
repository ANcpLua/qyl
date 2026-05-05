
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Alerting
{

    public partial class AlertRuleEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        [JsonPropertyName("rule_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AlertRuleType RuleType { get; set; }

        [JsonPropertyName("condition_json")]
        public string ConditionJson { get; set; }

        [JsonPropertyName("threshold_json")]
        public string ThresholdJson { get; set; }

        [JsonPropertyName("target_type")]
        public string TargetType { get; set; }

        [JsonPropertyName("target_filter_json")]
        public string TargetFilterJson { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AlertSeverity Severity { get; set; }

        [JsonPropertyName("cooldown_seconds")]
        public int CooldownSeconds { get; set; }

        [JsonPropertyName("notification_channels_json")]
        public string NotificationChannelsJson { get; set; }

        public bool Enabled { get; set; }

        [JsonPropertyName("last_triggered_at")]
        public DateTimeOffset? LastTriggeredAt { get; set; }

        [JsonPropertyName("trigger_count")]
        public long TriggerCount { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }


    }
}
