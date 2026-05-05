
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Ops.Deployment
{

    public partial class DeploymentEntity
    {
        [JsonPropertyName("deployment.id")]
        public string DeploymentId { get; set; }

        [JsonPropertyName("service.name")]
        public string ServiceName { get; set; }

        [StringConstraint(Pattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
        [JsonPropertyName("service.version")]
        public string ServiceVersion { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentEnvironment Environment { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentStatus Status { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DeploymentStrategy Strategy { get; set; }

        [JsonPropertyName("start_time")]
        public DateTimeOffset StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public DateTimeOffset? EndTime { get; set; }

        [JsonPropertyName("duration_s")]
        public double? DurationS { get; set; }

        [JsonPropertyName("deployed_by")]
        public string DeployedBy { get; set; }

        [JsonPropertyName("git_commit")]
        public string GitCommit { get; set; }

        [JsonPropertyName("git_branch")]
        public string GitBranch { get; set; }

        [StringConstraint(Pattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
        [JsonPropertyName("previous_version")]
        public string PreviousVersion { get; set; }

        [JsonPropertyName("rollback_target")]
        public string RollbackTarget { get; set; }

        [JsonPropertyName("replica_count")]
        public int? ReplicaCount { get; set; }

        [JsonPropertyName("healthy_replicas")]
        public int? HealthyReplicas { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }


    }
}
