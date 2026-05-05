
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Configurator
{
    public partial class GenerationJobEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal GenerationJobEntity(string id, string workspaceId, string profileId, GenerationJobType jobType, JobStatus status, int priority, DateTimeOffset queuedAt)
        {
            Id = id;
            WorkspaceId = workspaceId;
            ProfileId = profileId;
            JobType = jobType;
            Status = status;
            Priority = priority;
            QueuedAt = queuedAt;
        }

        internal GenerationJobEntity(string id, string workspaceId, string profileId, GenerationJobType jobType, JobStatus status, int priority, string inputHash, string outputPath, string outputHash, string errorMessage, DateTimeOffset queuedAt, DateTimeOffset? startedAt, DateTimeOffset? completedAt, int? durationMs, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            WorkspaceId = workspaceId;
            ProfileId = profileId;
            JobType = jobType;
            Status = status;
            Priority = priority;
            InputHash = inputHash;
            OutputPath = outputPath;
            OutputHash = outputHash;
            ErrorMessage = errorMessage;
            QueuedAt = queuedAt;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            DurationMs = durationMs;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string WorkspaceId { get; }

        public string ProfileId { get; }

        public GenerationJobType JobType { get; }

        public JobStatus Status { get; }

        public int Priority { get; }

        public string InputHash { get; }

        public string OutputPath { get; }

        public string OutputHash { get; }

        public string ErrorMessage { get; }

        public DateTimeOffset QueuedAt { get; }

        public DateTimeOffset? StartedAt { get; }

        public DateTimeOffset? CompletedAt { get; }

        public int? DurationMs { get; }
    }
}
