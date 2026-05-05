
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Domains.Alerting
{
    public partial class AlertRuleEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public AlertRuleEntity(string id, string projectId, string name, AlertRuleType ruleType, string conditionJson, string targetType, AlertSeverity severity, int cooldownSeconds, bool enabled, long triggerCount, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            Argument.AssertNotNull(id, nameof(id));
            Argument.AssertNotNull(projectId, nameof(projectId));
            Argument.AssertNotNull(name, nameof(name));
            Argument.AssertNotNull(conditionJson, nameof(conditionJson));
            Argument.AssertNotNull(targetType, nameof(targetType));

            Id = id;
            ProjectId = projectId;
            Name = name;
            RuleType = ruleType;
            ConditionJson = conditionJson;
            TargetType = targetType;
            Severity = severity;
            CooldownSeconds = cooldownSeconds;
            Enabled = enabled;
            TriggerCount = triggerCount;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        internal AlertRuleEntity(string id, string projectId, string name, string description, AlertRuleType ruleType, string conditionJson, string thresholdJson, string targetType, string targetFilterJson, AlertSeverity severity, int cooldownSeconds, string notificationChannelsJson, bool enabled, DateTimeOffset? lastTriggeredAt, long triggerCount, DateTimeOffset createdAt, DateTimeOffset updatedAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            ProjectId = projectId;
            Name = name;
            Description = description;
            RuleType = ruleType;
            ConditionJson = conditionJson;
            ThresholdJson = thresholdJson;
            TargetType = targetType;
            TargetFilterJson = targetFilterJson;
            Severity = severity;
            CooldownSeconds = cooldownSeconds;
            NotificationChannelsJson = notificationChannelsJson;
            Enabled = enabled;
            LastTriggeredAt = lastTriggeredAt;
            TriggerCount = triggerCount;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; set; }

        public string ProjectId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public AlertRuleType RuleType { get; set; }

        public string ConditionJson { get; set; }

        public string ThresholdJson { get; set; }

        public string TargetType { get; set; }

        public string TargetFilterJson { get; set; }

        public AlertSeverity Severity { get; set; }

        public int CooldownSeconds { get; set; }

        public string NotificationChannelsJson { get; set; }

        public bool Enabled { get; set; }

        public DateTimeOffset? LastTriggeredAt { get; set; }

        public long TriggerCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
