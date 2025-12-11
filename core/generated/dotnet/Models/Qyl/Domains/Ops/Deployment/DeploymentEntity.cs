
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Deployment
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class DeploymentEntity : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? DeployedBy { get; set; }
#nullable restore
#else
        public string DeployedBy { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? DeploymentId { get; set; }
#nullable restore
#else
        public string DeploymentId { get; set; }
#endif
                public double? DurationS { get; set; }
                public DateTimeOffset? EndTime { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEnvironment? Environment { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ErrorMessage { get; set; }
#nullable restore
#else
        public string ErrorMessage { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? GitBranch { get; set; }
#nullable restore
#else
        public string GitBranch { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? GitCommit { get; set; }
#nullable restore
#else
        public string GitCommit { get; set; }
#endif
                public int? HealthyReplicas { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? PreviousVersion { get; set; }
#nullable restore
#else
        public string PreviousVersion { get; set; }
#endif
                public int? ReplicaCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? RollbackTarget { get; set; }
#nullable restore
#else
        public string RollbackTarget { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ServiceName { get; set; }
#nullable restore
#else
        public string ServiceName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ServiceVersion { get; set; }
#nullable restore
#else
        public string ServiceVersion { get; set; }
#endif
                public DateTimeOffset? StartTime { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStatus? Status { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStrategy? Strategy { get; set; }
                public DeploymentEntity()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEntity();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "deployed_by", n => { DeployedBy = n.GetStringValue(); } },
                { "deployment.id", n => { DeploymentId = n.GetStringValue(); } },
                { "duration_s", n => { DurationS = n.GetDoubleValue(); } },
                { "end_time", n => { EndTime = n.GetDateTimeOffsetValue(); } },
                { "environment", n => { Environment = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEnvironment>(); } },
                { "error_message", n => { ErrorMessage = n.GetStringValue(); } },
                { "git_branch", n => { GitBranch = n.GetStringValue(); } },
                { "git_commit", n => { GitCommit = n.GetStringValue(); } },
                { "healthy_replicas", n => { HealthyReplicas = n.GetIntValue(); } },
                { "previous_version", n => { PreviousVersion = n.GetStringValue(); } },
                { "replica_count", n => { ReplicaCount = n.GetIntValue(); } },
                { "rollback_target", n => { RollbackTarget = n.GetStringValue(); } },
                { "service.name", n => { ServiceName = n.GetStringValue(); } },
                { "service.version", n => { ServiceVersion = n.GetStringValue(); } },
                { "start_time", n => { StartTime = n.GetDateTimeOffsetValue(); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStatus>(); } },
                { "strategy", n => { Strategy = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStrategy>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("deployed_by", DeployedBy);
            writer.WriteStringValue("deployment.id", DeploymentId);
            writer.WriteDoubleValue("duration_s", DurationS);
            writer.WriteDateTimeOffsetValue("end_time", EndTime);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEnvironment>("environment", Environment);
            writer.WriteStringValue("error_message", ErrorMessage);
            writer.WriteStringValue("git_branch", GitBranch);
            writer.WriteStringValue("git_commit", GitCommit);
            writer.WriteIntValue("healthy_replicas", HealthyReplicas);
            writer.WriteStringValue("previous_version", PreviousVersion);
            writer.WriteIntValue("replica_count", ReplicaCount);
            writer.WriteStringValue("rollback_target", RollbackTarget);
            writer.WriteStringValue("service.name", ServiceName);
            writer.WriteStringValue("service.version", ServiceVersion);
            writer.WriteDateTimeOffsetValue("start_time", StartTime);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStatus>("status", Status);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStrategy>("strategy", Strategy);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
