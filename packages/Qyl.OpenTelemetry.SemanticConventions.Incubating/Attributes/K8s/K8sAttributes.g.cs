

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.K8s;

public static class K8sAttributes
{
    public const string ClusterName = "k8s.cluster.name";

    public const string ClusterUid = "k8s.cluster.uid";

    public const string ContainerName = "k8s.container.name";

    public const string ContainerRestartCount = "k8s.container.restart_count";

    public const string ContainerStatusLastTerminatedReason = "k8s.container.status.last_terminated_reason";

    public const string ContainerStatusReason = "k8s.container.status.reason";

    public static class ContainerStatusReasonValues
    {
        public const string Completed = "Completed";

        public const string ContainerCannotRun = "ContainerCannotRun";

        public const string ContainerCreating = "ContainerCreating";

        public const string CrashLoopBackOff = "CrashLoopBackOff";

        public const string CreateContainerConfigError = "CreateContainerConfigError";

        public const string ErrImagePull = "ErrImagePull";

        public const string Error = "Error";

        public const string ImagePullBackOff = "ImagePullBackOff";

        public const string OomKilled = "OOMKilled";
    }

    public const string ContainerStatusState = "k8s.container.status.state";

    public static class ContainerStatusStateValues
    {
        public const string Running = "running";

        public const string Terminated = "terminated";

        public const string Waiting = "waiting";
    }

    public const string CronjobAnnotation = "k8s.cronjob.annotation";

    public const string CronjobLabel = "k8s.cronjob.label";

    public const string CronjobName = "k8s.cronjob.name";

    public const string CronjobUid = "k8s.cronjob.uid";

    public const string DaemonsetAnnotation = "k8s.daemonset.annotation";

    public const string DaemonsetLabel = "k8s.daemonset.label";

    public const string DaemonsetName = "k8s.daemonset.name";

    public const string DaemonsetUid = "k8s.daemonset.uid";

    public const string DeploymentAnnotation = "k8s.deployment.annotation";

    public const string DeploymentLabel = "k8s.deployment.label";

    public const string DeploymentName = "k8s.deployment.name";

    public const string DeploymentUid = "k8s.deployment.uid";

    public const string HpaMetricType = "k8s.hpa.metric.type";

    public const string HpaName = "k8s.hpa.name";

    public const string HpaScaletargetrefApiVersion = "k8s.hpa.scaletargetref.api_version";

    public const string HpaScaletargetrefKind = "k8s.hpa.scaletargetref.kind";

    public const string HpaScaletargetrefName = "k8s.hpa.scaletargetref.name";

    public const string HpaUid = "k8s.hpa.uid";

    public const string HugepageSize = "k8s.hugepage.size";

    public const string JobAnnotation = "k8s.job.annotation";

    public const string JobLabel = "k8s.job.label";

    public const string JobName = "k8s.job.name";

    public const string JobUid = "k8s.job.uid";

    public const string NamespaceAnnotation = "k8s.namespace.annotation";

    public const string NamespaceLabel = "k8s.namespace.label";

    public const string NamespaceName = "k8s.namespace.name";

    public const string NamespacePhase = "k8s.namespace.phase";

    public static class NamespacePhaseValues
    {
        public const string Active = "active";

        public const string Terminating = "terminating";
    }

    public const string NodeAnnotation = "k8s.node.annotation";

    public const string NodeConditionStatus = "k8s.node.condition.status";

    public static class NodeConditionStatusValues
    {
        public const string ConditionFalse = "false";

        public const string ConditionTrue = "true";

        public const string ConditionUnknown = "unknown";
    }

    public const string NodeConditionType = "k8s.node.condition.type";

    public static class NodeConditionTypeValues
    {
        public const string DiskPressure = "DiskPressure";

        public const string MemoryPressure = "MemoryPressure";

        public const string NetworkUnavailable = "NetworkUnavailable";

        public const string PidPressure = "PIDPressure";

        public const string Ready = "Ready";
    }

    public const string NodeLabel = "k8s.node.label";

    public const string NodeName = "k8s.node.name";

    public const string NodeSystemContainerName = "k8s.node.system_container.name";

    public const string NodeUid = "k8s.node.uid";

    public const string PersistentvolumeAnnotation = "k8s.persistentvolume.annotation";

    public const string PersistentvolumeLabel = "k8s.persistentvolume.label";

    public const string PersistentvolumeName = "k8s.persistentvolume.name";

    public const string PersistentvolumeReclaimPolicy = "k8s.persistentvolume.reclaim_policy";

    public static class PersistentvolumeReclaimPolicyValues
    {
        public const string Delete = "Delete";

        public const string Recycle = "Recycle";

        public const string Retain = "Retain";
    }

    public const string PersistentvolumeStatusPhase = "k8s.persistentvolume.status.phase";

    public static class PersistentvolumeStatusPhaseValues
    {
        public const string Available = "Available";

        public const string Bound = "Bound";

        public const string Failed = "Failed";

        public const string Pending = "Pending";

        public const string Released = "Released";
    }

    public const string PersistentvolumeUid = "k8s.persistentvolume.uid";

    public const string PersistentvolumeclaimAnnotation = "k8s.persistentvolumeclaim.annotation";

    public const string PersistentvolumeclaimLabel = "k8s.persistentvolumeclaim.label";

    public const string PersistentvolumeclaimName = "k8s.persistentvolumeclaim.name";

    public const string PersistentvolumeclaimStatusPhase = "k8s.persistentvolumeclaim.status.phase";

    public static class PersistentvolumeclaimStatusPhaseValues
    {
        public const string Bound = "Bound";

        public const string Lost = "Lost";

        public const string Pending = "Pending";
    }

    public const string PersistentvolumeclaimUid = "k8s.persistentvolumeclaim.uid";

    public const string PodAnnotation = "k8s.pod.annotation";

    public const string PodHostname = "k8s.pod.hostname";

    public const string PodIp = "k8s.pod.ip";

    public const string PodLabel = "k8s.pod.label";

    [global::System.Obsolete("Replaced by k8s.pod.label.", false)]
    public const string PodLabels = "k8s.pod.labels";

    public const string PodName = "k8s.pod.name";

    public const string PodStartTime = "k8s.pod.start_time";

    public const string PodStatusPhase = "k8s.pod.status.phase";

    public static class PodStatusPhaseValues
    {
        public const string Failed = "Failed";

        public const string Pending = "Pending";

        public const string Running = "Running";

        public const string Succeeded = "Succeeded";

        public const string Unknown = "Unknown";
    }

    public const string PodStatusReason = "k8s.pod.status.reason";

    public static class PodStatusReasonValues
    {
        public const string Evicted = "Evicted";

        public const string NodeAffinity = "NodeAffinity";

        public const string NodeLost = "NodeLost";

        public const string Shutdown = "Shutdown";

        public const string UnexpectedAdmissionError = "UnexpectedAdmissionError";
    }

    public const string PodUid = "k8s.pod.uid";

    public const string ReplicasetAnnotation = "k8s.replicaset.annotation";

    public const string ReplicasetLabel = "k8s.replicaset.label";

    public const string ReplicasetName = "k8s.replicaset.name";

    public const string ReplicasetUid = "k8s.replicaset.uid";

    public const string ReplicationcontrollerName = "k8s.replicationcontroller.name";

    public const string ReplicationcontrollerUid = "k8s.replicationcontroller.uid";

    public const string ResourcequotaName = "k8s.resourcequota.name";

    public const string ResourcequotaResourceName = "k8s.resourcequota.resource_name";

    public const string ResourcequotaUid = "k8s.resourcequota.uid";

    public const string ServiceAnnotation = "k8s.service.annotation";

    public const string ServiceEndpointAddressType = "k8s.service.endpoint.address_type";

    public static class ServiceEndpointAddressTypeValues
    {
        public const string Fqdn = "FQDN";

        public const string Ipv4 = "IPv4";

        public const string Ipv6 = "IPv6";
    }

    public const string ServiceEndpointCondition = "k8s.service.endpoint.condition";

    public static class ServiceEndpointConditionValues
    {
        public const string Ready = "ready";

        public const string Serving = "serving";

        public const string Terminating = "terminating";
    }

    public const string ServiceEndpointZone = "k8s.service.endpoint.zone";

    public const string ServiceLabel = "k8s.service.label";

    public const string ServiceName = "k8s.service.name";

    public const string ServicePublishNotReadyAddresses = "k8s.service.publish_not_ready_addresses";

    public const string ServiceSelector = "k8s.service.selector";

    public const string ServiceTrafficDistribution = "k8s.service.traffic_distribution";

    public const string ServiceType = "k8s.service.type";

    public static class ServiceTypeValues
    {
        public const string ClusterIp = "ClusterIP";

        public const string ExternalName = "ExternalName";

        public const string LoadBalancer = "LoadBalancer";

        public const string NodePort = "NodePort";
    }

    public const string ServiceUid = "k8s.service.uid";

    public const string StatefulsetAnnotation = "k8s.statefulset.annotation";

    public const string StatefulsetLabel = "k8s.statefulset.label";

    public const string StatefulsetName = "k8s.statefulset.name";

    public const string StatefulsetUid = "k8s.statefulset.uid";

    public const string StorageclassName = "k8s.storageclass.name";

    public const string VolumeName = "k8s.volume.name";

    public const string VolumeType = "k8s.volume.type";

    public static class VolumeTypeValues
    {
        public const string ConfigMap = "configMap";

        public const string DownwardApi = "downwardAPI";

        public const string EmptyDir = "emptyDir";

        public const string Local = "local";

        public const string PersistentVolumeClaim = "persistentVolumeClaim";

        public const string Secret = "secret";
    }
}
