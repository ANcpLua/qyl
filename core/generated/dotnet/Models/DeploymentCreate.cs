
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Domains.Ops.Deployment;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class DeploymentCreate : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? DeployedBy { get; set; }
#nullable restore
#else
        public string DeployedBy { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEnvironment? Environment { get; set; }
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
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStrategy? Strategy { get; set; }
                public DeploymentCreate()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.DeploymentCreate CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.DeploymentCreate();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "deployed_by", n => { DeployedBy = n.GetStringValue(); } },
                { "environment", n => { Environment = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEnvironment>(); } },
                { "git_branch", n => { GitBranch = n.GetStringValue(); } },
                { "git_commit", n => { GitCommit = n.GetStringValue(); } },
                { "service_name", n => { ServiceName = n.GetStringValue(); } },
                { "service_version", n => { ServiceVersion = n.GetStringValue(); } },
                { "strategy", n => { Strategy = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStrategy>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("deployed_by", DeployedBy);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentEnvironment>("environment", Environment);
            writer.WriteStringValue("git_branch", GitBranch);
            writer.WriteStringValue("git_commit", GitCommit);
            writer.WriteStringValue("service_name", ServiceName);
            writer.WriteStringValue("service_version", ServiceVersion);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Deployment.DeploymentStrategy>("strategy", Strategy);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
