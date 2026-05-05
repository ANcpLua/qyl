
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Workflow
{
    public partial class WorkflowNodeEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal WorkflowNodeEntity(string id, string runId, string nodeId, WorkflowNodeType nodeType, string nodeName, int attempt, WorkflowRunStatus status, int retryCount, int maxRetries, DateTimeOffset createdAt)
        {
            Id = id;
            RunId = runId;
            NodeId = nodeId;
            NodeType = nodeType;
            NodeName = nodeName;
            Attempt = attempt;
            Status = status;
            RetryCount = retryCount;
            MaxRetries = maxRetries;
            CreatedAt = createdAt;
        }

        internal WorkflowNodeEntity(string id, string runId, string nodeId, WorkflowNodeType nodeType, string nodeName, int attempt, string inputJson, string outputJson, WorkflowRunStatus status, string errorMessage, int retryCount, int maxRetries, int? timeoutMs, DateTimeOffset? startedAt, DateTimeOffset? completedAt, int? durationMs, DateTimeOffset createdAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            RunId = runId;
            NodeId = nodeId;
            NodeType = nodeType;
            NodeName = nodeName;
            Attempt = attempt;
            InputJson = inputJson;
            OutputJson = outputJson;
            Status = status;
            ErrorMessage = errorMessage;
            RetryCount = retryCount;
            MaxRetries = maxRetries;
            TimeoutMs = timeoutMs;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            DurationMs = durationMs;
            CreatedAt = createdAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string RunId { get; }

        public string NodeId { get; }

        public WorkflowNodeType NodeType { get; }

        public string NodeName { get; }

        public int Attempt { get; }

        public string InputJson { get; }

        public string OutputJson { get; }

        public WorkflowRunStatus Status { get; }

        public string ErrorMessage { get; }

        public int RetryCount { get; }

        public int MaxRetries { get; }

        public int? TimeoutMs { get; }

        public DateTimeOffset? StartedAt { get; }

        public DateTimeOffset? CompletedAt { get; }

        public int? DurationMs { get; }

        public DateTimeOffset CreatedAt { get; }
    }
}
