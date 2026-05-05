
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Alerting
{

    public partial class AlertFiringEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("rule_id")]
        public string RuleId { get; set; }

        public string Fingerprint { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AlertSeverity Severity { get; set; }

        public string Title { get; set; }

        public string Message { get; set; }

        [JsonPropertyName("trigger_value")]
        public double? TriggerValue { get; set; }

        [JsonPropertyName("threshold_value")]
        public double? ThresholdValue { get; set; }

        [JsonPropertyName("context_json")]
        public string ContextJson { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AlertFiringStatus Status { get; set; }

        [JsonPropertyName("acknowledged_at")]
        public DateTimeOffset? AcknowledgedAt { get; set; }

        [JsonPropertyName("acknowledged_by")]
        public string AcknowledgedBy { get; set; }

        [JsonPropertyName("resolved_at")]
        public DateTimeOffset? ResolvedAt { get; set; }

        [JsonPropertyName("fired_at")]
        public DateTimeOffset FiredAt { get; set; }

        [JsonPropertyName("dedup_key")]
        public string DedupKey { get; set; }


    }
}
