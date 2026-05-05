
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Workflow
{
    public partial class WorkflowRunEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal WorkflowRunEntity(string id, string workflowId, int workflowVersion, string projectId, WorkflowTriggerType triggerType, WorkflowRunStatus status, DateTimeOffset createdAt)
        {
            Id = id;
            WorkflowId = workflowId;
            WorkflowVersion = workflowVersion;
            ProjectId = projectId;
            TriggerType = triggerType;
            Status = status;
            CreatedAt = createdAt;
        }

        internal WorkflowRunEntity(string id, string workflowId, int workflowVersion, string projectId, WorkflowTriggerType triggerType, string triggerSource, string inputJson, string outputJson, WorkflowRunStatus status, string errorMessage, string parentRunId, string correlationId, DateTimeOffset? startedAt, DateTimeOffset? completedAt, int? durationMs, DateTimeOffset createdAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            WorkflowId = workflowId;
            WorkflowVersion = workflowVersion;
            ProjectId = projectId;
            TriggerType = triggerType;
            TriggerSource = triggerSource;
            InputJson = inputJson;
            OutputJson = outputJson;
            Status = status;
            ErrorMessage = errorMessage;
            ParentRunId = parentRunId;
            CorrelationId = correlationId;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            DurationMs = durationMs;
            CreatedAt = createdAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string WorkflowId { get; }

        public int WorkflowVersion { get; }

        public string ProjectId { get; }

        public WorkflowTriggerType TriggerType { get; }

        public string TriggerSource { get; }

        public string InputJson { get; }

        public string OutputJson { get; }

        public WorkflowRunStatus Status { get; }

        public string ErrorMessage { get; }

        public string ParentRunId { get; }

        public string CorrelationId { get; }

        public DateTimeOffset? StartedAt { get; }

        public DateTimeOffset? CompletedAt { get; }

        public int? DurationMs { get; }

        public DateTimeOffset CreatedAt { get; }
    }
}
