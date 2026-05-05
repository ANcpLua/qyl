
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Ops.Deployment
{
    public partial class DeploymentEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal DeploymentEntity(string deploymentId, string serviceName, string serviceVersion, DeploymentEnvironment environment, DeploymentStatus status, DeploymentStrategy strategy, DateTimeOffset startTime)
        {
            DeploymentId = deploymentId;
            ServiceName = serviceName;
            ServiceVersion = serviceVersion;
            Environment = environment;
            Status = status;
            Strategy = strategy;
            StartTime = startTime;
        }

        internal DeploymentEntity(string deploymentId, string serviceName, string serviceVersion, DeploymentEnvironment environment, DeploymentStatus status, DeploymentStrategy strategy, DateTimeOffset startTime, DateTimeOffset? endTime, double? durationS, string deployedBy, string gitCommit, string gitBranch, string previousVersion, string rollbackTarget, int? replicaCount, int? healthyReplicas, string errorMessage, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            DeploymentId = deploymentId;
            ServiceName = serviceName;
            ServiceVersion = serviceVersion;
            Environment = environment;
            Status = status;
            Strategy = strategy;
            StartTime = startTime;
            EndTime = endTime;
            DurationS = durationS;
            DeployedBy = deployedBy;
            GitCommit = gitCommit;
            GitBranch = gitBranch;
            PreviousVersion = previousVersion;
            RollbackTarget = rollbackTarget;
            ReplicaCount = replicaCount;
            HealthyReplicas = healthyReplicas;
            ErrorMessage = errorMessage;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string DeploymentId { get; }

        public string ServiceName { get; }

        public string ServiceVersion { get; }

        public DeploymentEnvironment Environment { get; }

        public DeploymentStatus Status { get; }

        public DeploymentStrategy Strategy { get; }

        public DateTimeOffset StartTime { get; }

        public DateTimeOffset? EndTime { get; }

        public double? DurationS { get; }

        public string DeployedBy { get; }

        public string GitCommit { get; }

        public string GitBranch { get; }

        public string PreviousVersion { get; }

        public string RollbackTarget { get; }

        public int? ReplicaCount { get; }

        public int? HealthyReplicas { get; }

        public string ErrorMessage { get; }
    }
}
