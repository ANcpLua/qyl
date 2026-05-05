
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Alerting
{
    public partial class AlertFiringEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal AlertFiringEntity(string id, string ruleId, string fingerprint, AlertSeverity severity, string title, AlertFiringStatus status, DateTimeOffset firedAt)
        {
            Id = id;
            RuleId = ruleId;
            Fingerprint = fingerprint;
            Severity = severity;
            Title = title;
            Status = status;
            FiredAt = firedAt;
        }

        internal AlertFiringEntity(string id, string ruleId, string fingerprint, AlertSeverity severity, string title, string message, double? triggerValue, double? thresholdValue, string contextJson, AlertFiringStatus status, DateTimeOffset? acknowledgedAt, string acknowledgedBy, DateTimeOffset? resolvedAt, DateTimeOffset firedAt, string dedupKey, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            RuleId = ruleId;
            Fingerprint = fingerprint;
            Severity = severity;
            Title = title;
            Message = message;
            TriggerValue = triggerValue;
            ThresholdValue = thresholdValue;
            ContextJson = contextJson;
            Status = status;
            AcknowledgedAt = acknowledgedAt;
            AcknowledgedBy = acknowledgedBy;
            ResolvedAt = resolvedAt;
            FiredAt = firedAt;
            DedupKey = dedupKey;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string RuleId { get; }

        public string Fingerprint { get; }

        public AlertSeverity Severity { get; }

        public string Title { get; }

        public string Message { get; }

        public double? TriggerValue { get; }

        public double? ThresholdValue { get; }

        public string ContextJson { get; }

        public AlertFiringStatus Status { get; }

        public DateTimeOffset? AcknowledgedAt { get; }

        public string AcknowledgedBy { get; }

        public DateTimeOffset? ResolvedAt { get; }

        public DateTimeOffset FiredAt { get; }

        public string DedupKey { get; }
    }
}
