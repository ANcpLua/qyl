
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Common;
using Qyl.Core.Models.Qyl.OTel.Enums;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.OTel.Resource
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class Resource : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject>? Attributes { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject> Attributes { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? CloudAccountId { get; set; }
#nullable restore
#else
        public string CloudAccountId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? CloudAvailabilityZone { get; set; }
#nullable restore
#else
        public string CloudAvailabilityZone { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? CloudPlatform { get; set; }
#nullable restore
#else
        public string CloudPlatform { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.OTel.Resource.CloudProvider? CloudProvider { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? CloudRegion { get; set; }
#nullable restore
#else
        public string CloudRegion { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ContainerId { get; set; }
#nullable restore
#else
        public string ContainerId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ContainerImageName { get; set; }
#nullable restore
#else
        public string ContainerImageName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ContainerImageTag { get; set; }
#nullable restore
#else
        public string ContainerImageTag { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ContainerName { get; set; }
#nullable restore
#else
        public string ContainerName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? DeploymentEnvironmentName { get; set; }
#nullable restore
#else
        public string DeploymentEnvironmentName { get; set; }
#endif
                public long? DroppedAttributesCount { get; set; }
                public global::Qyl.Core.Models.Qyl.OTel.Resource.HostArch? HostArch { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? HostId { get; set; }
#nullable restore
#else
        public string HostId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? HostName { get; set; }
#nullable restore
#else
        public string HostName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? HostType { get; set; }
#nullable restore
#else
        public string HostType { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? K8sClusterName { get; set; }
#nullable restore
#else
        public string K8sClusterName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? K8sDeploymentName { get; set; }
#nullable restore
#else
        public string K8sDeploymentName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? K8sNamespaceName { get; set; }
#nullable restore
#else
        public string K8sNamespaceName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? K8sPodName { get; set; }
#nullable restore
#else
        public string K8sPodName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? K8sPodUid { get; set; }
#nullable restore
#else
        public string K8sPodUid { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? OsDescription { get; set; }
#nullable restore
#else
        public string OsDescription { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.OTel.Resource.OsType? OsType { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? OsVersion { get; set; }
#nullable restore
#else
        public string OsVersion { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ProcessCommandLine { get; set; }
#nullable restore
#else
        public string ProcessCommandLine { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ProcessExecutableName { get; set; }
#nullable restore
#else
        public string ProcessExecutableName { get; set; }
#endif
                public long? ProcessPid { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ProcessRuntimeName { get; set; }
#nullable restore
#else
        public string ProcessRuntimeName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ProcessRuntimeVersion { get; set; }
#nullable restore
#else
        public string ProcessRuntimeVersion { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ServiceInstanceId { get; set; }
#nullable restore
#else
        public string ServiceInstanceId { get; set; }
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
        public string? ServiceNamespace { get; set; }
#nullable restore
#else
        public string ServiceNamespace { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ServiceVersion { get; set; }
#nullable restore
#else
        public string ServiceVersion { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TelemetryAutoVersion { get; set; }
#nullable restore
#else
        public string TelemetryAutoVersion { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.OTel.Enums.TelemetrySdkLanguage? TelemetrySdkLanguage { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TelemetrySdkName { get; set; }
#nullable restore
#else
        public string TelemetrySdkName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TelemetrySdkVersion { get; set; }
#nullable restore
#else
        public string TelemetrySdkVersion { get; set; }
#endif
                public Resource()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.OTel.Resource.Resource CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.OTel.Resource.Resource();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "attributes", n => { Attributes = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>(global::Qyl.Core.Models.Qyl.Common.AttributeObject.CreateFromDiscriminatorValue)?.AsList(); } },
                { "cloud.account.id", n => { CloudAccountId = n.GetStringValue(); } },
                { "cloud.availability_zone", n => { CloudAvailabilityZone = n.GetStringValue(); } },
                { "cloud.platform", n => { CloudPlatform = n.GetStringValue(); } },
                { "cloud.provider", n => { CloudProvider = n.GetEnumValue<global::Qyl.Core.Models.Qyl.OTel.Resource.CloudProvider>(); } },
                { "cloud.region", n => { CloudRegion = n.GetStringValue(); } },
                { "container.id", n => { ContainerId = n.GetStringValue(); } },
                { "container.image.name", n => { ContainerImageName = n.GetStringValue(); } },
                { "container.image.tag", n => { ContainerImageTag = n.GetStringValue(); } },
                { "container.name", n => { ContainerName = n.GetStringValue(); } },
                { "deployment.environment.name", n => { DeploymentEnvironmentName = n.GetStringValue(); } },
                { "dropped_attributes_count", n => { DroppedAttributesCount = n.GetLongValue(); } },
                { "host.arch", n => { HostArch = n.GetEnumValue<global::Qyl.Core.Models.Qyl.OTel.Resource.HostArch>(); } },
                { "host.id", n => { HostId = n.GetStringValue(); } },
                { "host.name", n => { HostName = n.GetStringValue(); } },
                { "host.type", n => { HostType = n.GetStringValue(); } },
                { "k8s.cluster.name", n => { K8sClusterName = n.GetStringValue(); } },
                { "k8s.deployment.name", n => { K8sDeploymentName = n.GetStringValue(); } },
                { "k8s.namespace.name", n => { K8sNamespaceName = n.GetStringValue(); } },
                { "k8s.pod.name", n => { K8sPodName = n.GetStringValue(); } },
                { "k8s.pod.uid", n => { K8sPodUid = n.GetStringValue(); } },
                { "os.description", n => { OsDescription = n.GetStringValue(); } },
                { "os.type", n => { OsType = n.GetEnumValue<global::Qyl.Core.Models.Qyl.OTel.Resource.OsType>(); } },
                { "os.version", n => { OsVersion = n.GetStringValue(); } },
                { "process.command_line", n => { ProcessCommandLine = n.GetStringValue(); } },
                { "process.executable.name", n => { ProcessExecutableName = n.GetStringValue(); } },
                { "process.pid", n => { ProcessPid = n.GetLongValue(); } },
                { "process.runtime.name", n => { ProcessRuntimeName = n.GetStringValue(); } },
                { "process.runtime.version", n => { ProcessRuntimeVersion = n.GetStringValue(); } },
                { "service.instance.id", n => { ServiceInstanceId = n.GetStringValue(); } },
                { "service.name", n => { ServiceName = n.GetStringValue(); } },
                { "service.namespace", n => { ServiceNamespace = n.GetStringValue(); } },
                { "service.version", n => { ServiceVersion = n.GetStringValue(); } },
                { "telemetry.auto.version", n => { TelemetryAutoVersion = n.GetStringValue(); } },
                { "telemetry.sdk.language", n => { TelemetrySdkLanguage = n.GetEnumValue<global::Qyl.Core.Models.Qyl.OTel.Enums.TelemetrySdkLanguage>(); } },
                { "telemetry.sdk.name", n => { TelemetrySdkName = n.GetStringValue(); } },
                { "telemetry.sdk.version", n => { TelemetrySdkVersion = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>("attributes", Attributes);
            writer.WriteStringValue("cloud.account.id", CloudAccountId);
            writer.WriteStringValue("cloud.availability_zone", CloudAvailabilityZone);
            writer.WriteStringValue("cloud.platform", CloudPlatform);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.OTel.Resource.CloudProvider>("cloud.provider", CloudProvider);
            writer.WriteStringValue("cloud.region", CloudRegion);
            writer.WriteStringValue("container.id", ContainerId);
            writer.WriteStringValue("container.image.name", ContainerImageName);
            writer.WriteStringValue("container.image.tag", ContainerImageTag);
            writer.WriteStringValue("container.name", ContainerName);
            writer.WriteStringValue("deployment.environment.name", DeploymentEnvironmentName);
            writer.WriteLongValue("dropped_attributes_count", DroppedAttributesCount);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.OTel.Resource.HostArch>("host.arch", HostArch);
            writer.WriteStringValue("host.id", HostId);
            writer.WriteStringValue("host.name", HostName);
            writer.WriteStringValue("host.type", HostType);
            writer.WriteStringValue("k8s.cluster.name", K8sClusterName);
            writer.WriteStringValue("k8s.deployment.name", K8sDeploymentName);
            writer.WriteStringValue("k8s.namespace.name", K8sNamespaceName);
            writer.WriteStringValue("k8s.pod.name", K8sPodName);
            writer.WriteStringValue("k8s.pod.uid", K8sPodUid);
            writer.WriteStringValue("os.description", OsDescription);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.OTel.Resource.OsType>("os.type", OsType);
            writer.WriteStringValue("os.version", OsVersion);
            writer.WriteStringValue("process.command_line", ProcessCommandLine);
            writer.WriteStringValue("process.executable.name", ProcessExecutableName);
            writer.WriteLongValue("process.pid", ProcessPid);
            writer.WriteStringValue("process.runtime.name", ProcessRuntimeName);
            writer.WriteStringValue("process.runtime.version", ProcessRuntimeVersion);
            writer.WriteStringValue("service.instance.id", ServiceInstanceId);
            writer.WriteStringValue("service.name", ServiceName);
            writer.WriteStringValue("service.namespace", ServiceNamespace);
            writer.WriteStringValue("service.version", ServiceVersion);
            writer.WriteStringValue("telemetry.auto.version", TelemetryAutoVersion);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.OTel.Enums.TelemetrySdkLanguage>("telemetry.sdk.language", TelemetrySdkLanguage);
            writer.WriteStringValue("telemetry.sdk.name", TelemetrySdkName);
            writer.WriteStringValue("telemetry.sdk.version", TelemetrySdkVersion);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
