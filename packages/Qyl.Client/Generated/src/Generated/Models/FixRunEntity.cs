
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Alerting
{
    public partial class FixRunEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal FixRunEntity(string id, string issueId, FixTriggerType triggerType, string strategy, FixRunStatus status, DateTimeOffset createdAt)
        {
            Id = id;
            IssueId = issueId;
            TriggerType = triggerType;
            Strategy = strategy;
            Status = status;
            CreatedAt = createdAt;
        }

        internal FixRunEntity(string id, string issueId, string alertFiringId, FixTriggerType triggerType, string strategy, string modelName, string modelProvider, FixRunStatus status, string errorMessage, int? tokensUsed, int? durationMs, DateTimeOffset createdAt, DateTimeOffset? startedAt, DateTimeOffset? completedAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            IssueId = issueId;
            AlertFiringId = alertFiringId;
            TriggerType = triggerType;
            Strategy = strategy;
            ModelName = modelName;
            ModelProvider = modelProvider;
            Status = status;
            ErrorMessage = errorMessage;
            TokensUsed = tokensUsed;
            DurationMs = durationMs;
            CreatedAt = createdAt;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string IssueId { get; }

        public string AlertFiringId { get; }

        public FixTriggerType TriggerType { get; }

        public string Strategy { get; }

        public string ModelName { get; }

        public string ModelProvider { get; }

        public FixRunStatus Status { get; }

        public string ErrorMessage { get; }

        public int? TokensUsed { get; }

        public int? DurationMs { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset? StartedAt { get; }

        public DateTimeOffset? CompletedAt { get; }
    }
}
