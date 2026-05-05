
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.Common;
using Qyl.OTel.Enums;

namespace Qyl.OTel.Resource
{
    public partial class Resource
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal Resource(string serviceName)
        {
            ServiceName = serviceName;
            Attributes = new ChangeTrackingList<Common.Attribute>();
        }

        internal Resource(string serviceName, string serviceNamespace, string serviceInstanceId, string serviceVersion, string telemetrySdkName, TelemetrySdkLanguage? telemetrySdkLanguage, string telemetrySdkVersion, string telemetryAutoVersion, string deploymentEnvironment, CloudProvider? cloudProvider, string cloudRegion, string cloudAvailabilityZone, string cloudAccountId, string cloudPlatform, string hostName, string hostId, string hostType, HostArch? hostArch, OsType? osType, string osDescription, string osVersion, long? processPid, string processExecutableName, string processCommandLine, string processRuntimeName, string processRuntimeVersion, string containerId, string containerName, string containerImageName, string containerImageTag, string k8sClusterName, string k8sNamespaceName, string k8sPodName, string k8sPodUid, string k8sDeploymentName, IList<Common.Attribute> attributes, long? droppedAttributesCount, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ServiceName = serviceName;
            ServiceNamespace = serviceNamespace;
            ServiceInstanceId = serviceInstanceId;
            ServiceVersion = serviceVersion;
            TelemetrySdkName = telemetrySdkName;
            TelemetrySdkLanguage = telemetrySdkLanguage;
            TelemetrySdkVersion = telemetrySdkVersion;
            TelemetryAutoVersion = telemetryAutoVersion;
            DeploymentEnvironment = deploymentEnvironment;
            CloudProvider = cloudProvider;
            CloudRegion = cloudRegion;
            CloudAvailabilityZone = cloudAvailabilityZone;
            CloudAccountId = cloudAccountId;
            CloudPlatform = cloudPlatform;
            HostName = hostName;
            HostId = hostId;
            HostType = hostType;
            HostArch = hostArch;
            OsType = osType;
            OsDescription = osDescription;
            OsVersion = osVersion;
            ProcessPid = processPid;
            ProcessExecutableName = processExecutableName;
            ProcessCommandLine = processCommandLine;
            ProcessRuntimeName = processRuntimeName;
            ProcessRuntimeVersion = processRuntimeVersion;
            ContainerId = containerId;
            ContainerName = containerName;
            ContainerImageName = containerImageName;
            ContainerImageTag = containerImageTag;
            K8sClusterName = k8sClusterName;
            K8sNamespaceName = k8sNamespaceName;
            K8sPodName = k8sPodName;
            K8sPodUid = k8sPodUid;
            K8sDeploymentName = k8sDeploymentName;
            Attributes = attributes;
            DroppedAttributesCount = droppedAttributesCount;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ServiceName { get; }

        public string ServiceNamespace { get; }

        public string ServiceInstanceId { get; }

        public string ServiceVersion { get; }

        public string TelemetrySdkName { get; }

        public TelemetrySdkLanguage? TelemetrySdkLanguage { get; }

        public string TelemetrySdkVersion { get; }

        public string TelemetryAutoVersion { get; }

        public string DeploymentEnvironment { get; }

        public CloudProvider? CloudProvider { get; }

        public string CloudRegion { get; }

        public string CloudAvailabilityZone { get; }

        public string CloudAccountId { get; }

        public string CloudPlatform { get; }

        public string HostName { get; }

        public string HostId { get; }

        public string HostType { get; }

        public HostArch? HostArch { get; }

        public OsType? OsType { get; }

        public string OsDescription { get; }

        public string OsVersion { get; }

        public long? ProcessPid { get; }

        public string ProcessExecutableName { get; }

        public string ProcessCommandLine { get; }

        public string ProcessRuntimeName { get; }

        public string ProcessRuntimeVersion { get; }

        public string ContainerId { get; }

        public string ContainerName { get; }

        public string ContainerImageName { get; }

        public string ContainerImageTag { get; }

        public string K8sClusterName { get; }

        public string K8sNamespaceName { get; }

        public string K8sPodName { get; }

        public string K8sPodUid { get; }

        public string K8sDeploymentName { get; }

        public IList<Common.Attribute> Attributes { get; }

        public long? DroppedAttributesCount { get; }
    }
}
