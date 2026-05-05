
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Workspace
{
    public partial class WorkspaceEnvelopeEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal WorkspaceEnvelopeEntity(string id, string projectId, string environmentId, string nodeId, string name, string rootPath, int heartbeatIntervalSeconds, WorkspaceStatus status, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            Id = id;
            ProjectId = projectId;
            EnvironmentId = environmentId;
            NodeId = nodeId;
            Name = name;
            RootPath = rootPath;
            HeartbeatIntervalSeconds = heartbeatIntervalSeconds;
            Status = status;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        internal WorkspaceEnvelopeEntity(string id, string projectId, string environmentId, string nodeId, string name, string rootPath, DateTimeOffset? heartbeatAt, int heartbeatIntervalSeconds, WorkspaceStatus status, string configJson, DateTimeOffset createdAt, DateTimeOffset updatedAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            ProjectId = projectId;
            EnvironmentId = environmentId;
            NodeId = nodeId;
            Name = name;
            RootPath = rootPath;
            HeartbeatAt = heartbeatAt;
            HeartbeatIntervalSeconds = heartbeatIntervalSeconds;
            Status = status;
            ConfigJson = configJson;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string ProjectId { get; }

        public string EnvironmentId { get; }

        public string NodeId { get; }

        public string Name { get; }

        public string RootPath { get; }

        public DateTimeOffset? HeartbeatAt { get; }

        public int HeartbeatIntervalSeconds { get; }

        public WorkspaceStatus Status { get; }

        public string ConfigJson { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; }
    }
}
