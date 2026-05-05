
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;
using Qyl.Common;

namespace Qyl.OTel.Resource
{

    public partial class Resource
    {
        [JsonPropertyName("service.name")]
        public string ServiceName { get; set; }

        [JsonPropertyName("service.namespace")]
        public string ServiceNamespace { get; set; }

        [JsonPropertyName("service.instance.id")]
        public string ServiceInstanceId { get; set; }

        [StringConstraint(Pattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
        [JsonPropertyName("service.version")]
        public string ServiceVersion { get; set; }

        [JsonPropertyName("telemetry.sdk.name")]
        public string TelemetrySdkName { get; set; }

        [JsonPropertyName("telemetry.sdk.language")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TelemetrySdkLanguage? TelemetrySdkLanguage { get; set; }

        [StringConstraint(Pattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
        [JsonPropertyName("telemetry.sdk.version")]
        public string TelemetrySdkVersion { get; set; }

        [StringConstraint(Pattern = @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
        [JsonPropertyName("telemetry.auto.version")]
        public string TelemetryAutoVersion { get; set; }

        [JsonPropertyName("deployment.environment.name")]
        public string DeploymentEnvironment { get; set; }

        [JsonPropertyName("cloud.provider")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CloudProvider? CloudProvider { get; set; }

        [JsonPropertyName("cloud.region")]
        public string CloudRegion { get; set; }

        [JsonPropertyName("cloud.availability_zone")]
        public string CloudAvailabilityZone { get; set; }

        [JsonPropertyName("cloud.account.id")]
        public string CloudAccountId { get; set; }

        [JsonPropertyName("cloud.platform")]
        public string CloudPlatform { get; set; }

        [JsonPropertyName("host.name")]
        public string HostName { get; set; }

        [JsonPropertyName("host.id")]
        public string HostId { get; set; }

        [JsonPropertyName("host.type")]
        public string HostType { get; set; }

        [JsonPropertyName("host.arch")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HostArch? HostArch { get; set; }

        [JsonPropertyName("os.type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OsType? OsType { get; set; }

        [JsonPropertyName("os.description")]
        public string OsDescription { get; set; }

        [JsonPropertyName("os.version")]
        public string OsVersion { get; set; }

        [JsonPropertyName("process.pid")]
        public long? ProcessPid { get; set; }

        [JsonPropertyName("process.executable.name")]
        public string ProcessExecutableName { get; set; }

        [JsonPropertyName("process.command_line")]
        public string ProcessCommandLine { get; set; }

        [JsonPropertyName("process.runtime.name")]
        public string ProcessRuntimeName { get; set; }

        [JsonPropertyName("process.runtime.version")]
        public string ProcessRuntimeVersion { get; set; }

        [JsonPropertyName("container.id")]
        public string ContainerId { get; set; }

        [JsonPropertyName("container.name")]
        public string ContainerName { get; set; }

        [JsonPropertyName("container.image.name")]
        public string ContainerImageName { get; set; }

        [JsonPropertyName("container.image.tag")]
        public string ContainerImageTag { get; set; }

        [JsonPropertyName("k8s.cluster.name")]
        public string K8sClusterName { get; set; }

        [JsonPropertyName("k8s.namespace.name")]
        public string K8sNamespaceName { get; set; }

        [JsonPropertyName("k8s.pod.name")]
        public string K8sPodName { get; set; }

        [JsonPropertyName("k8s.pod.uid")]
        public string K8sPodUid { get; set; }

        [JsonPropertyName("k8s.deployment.name")]
        public string K8sDeploymentName { get; set; }

        public Attribute[] Attributes { get; set; }

        [JsonPropertyName("dropped_attributes_count")]
        public long? DroppedAttributesCount { get; set; }


    }
}
