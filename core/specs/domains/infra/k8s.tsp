// =============================================================================
// QYL v2.0 - Kubernetes Semantic Conventions (OTel v1.38)
// =============================================================================
// Kubernetes resource and workload attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Infra.K8s;

// =============================================================================
// Kubernetes Resource Attributes
// =============================================================================

@doc("Kubernetes resource attributes")
model K8sAttributes {
  // ---------------------------------------------------------------------------
  // Cluster
  // ---------------------------------------------------------------------------

  @doc("Kubernetes cluster name")
  @encodedName("application/json", "k8s.cluster.name")
  clusterName?: string;

  @doc("Kubernetes cluster UID")
  @encodedName("application/json", "k8s.cluster.uid")
  clusterUid?: string;

  // ---------------------------------------------------------------------------
  // Namespace
  // ---------------------------------------------------------------------------

  @doc("Kubernetes namespace name")
  @encodedName("application/json", "k8s.namespace.name")
  namespaceName?: string;

  @doc("Kubernetes namespace UID")
  @encodedName("application/json", "k8s.namespace.uid")
  namespaceUid?: string;

  // ---------------------------------------------------------------------------
  // Pod
  // ---------------------------------------------------------------------------

  @doc("Pod name")
  @encodedName("application/json", "k8s.pod.name")
  podName?: string;

  @doc("Pod UID")
  @encodedName("application/json", "k8s.pod.uid")
  podUid?: string;

  @doc("Pod labels. Keys map to k8s.pod.label.<key>")
  @encodedName("application/json", "k8s.pod.label")
  podLabels?: Record<string>;

  @doc("Pod annotations. Keys map to k8s.pod.annotation.<key>")
  @encodedName("application/json", "k8s.pod.annotation")
  podAnnotations?: Record<string>;

  @doc("Pod IP address")
  @encodedName("application/json", "k8s.pod.ip")
  podIp?: IpAddress;

  // ---------------------------------------------------------------------------
  // Container
  // ---------------------------------------------------------------------------

  @doc("Container name within the pod")
  @encodedName("application/json", "k8s.container.name")
  containerName?: string;

  @doc("Container restart count")
  @encodedName("application/json", "k8s.container.restart_count")
  containerRestartCount?: int32;

  @doc("Container status reason")
  @encodedName("application/json", "k8s.container.status.reason")
  containerStatusReason?: string;

  // ---------------------------------------------------------------------------
  // Workload Controllers
  // ---------------------------------------------------------------------------

  @doc("Deployment name")
  @encodedName("application/json", "k8s.deployment.name")
  deploymentName?: string;

  @doc("Deployment UID")
  @encodedName("application/json", "k8s.deployment.uid")
  deploymentUid?: string;

  @doc("ReplicaSet name")
  @encodedName("application/json", "k8s.replicaset.name")
  replicasetName?: string;

  @doc("ReplicaSet UID")
  @encodedName("application/json", "k8s.replicaset.uid")
  replicasetUid?: string;

  @doc("StatefulSet name")
  @encodedName("application/json", "k8s.statefulset.name")
  statefulsetName?: string;

  @doc("StatefulSet UID")
  @encodedName("application/json", "k8s.statefulset.uid")
  statefulsetUid?: string;

  @doc("DaemonSet name")
  @encodedName("application/json", "k8s.daemonset.name")
  daemonsetName?: string;

  @doc("DaemonSet UID")
  @encodedName("application/json", "k8s.daemonset.uid")
  daemonsetUid?: string;

  @doc("Job name")
  @encodedName("application/json", "k8s.job.name")
  jobName?: string;

  @doc("Job UID")
  @encodedName("application/json", "k8s.job.uid")
  jobUid?: string;

  @doc("CronJob name")
  @encodedName("application/json", "k8s.cronjob.name")
  cronjobName?: string;

  @doc("CronJob UID")
  @encodedName("application/json", "k8s.cronjob.uid")
  cronjobUid?: string;

  // ---------------------------------------------------------------------------
  // Node
  // ---------------------------------------------------------------------------

  @doc("Node name")
  @encodedName("application/json", "k8s.node.name")
  nodeName?: string;

  @doc("Node UID")
  @encodedName("application/json", "k8s.node.uid")
  nodeUid?: string;
}

// =============================================================================
// Kubernetes Enumerations
// =============================================================================

@doc("Kubernetes workload kinds")
enum K8sWorkloadKind {
  @doc("Deployment")
  deployment: "Deployment",

  @doc("ReplicaSet")
  replicaset: "ReplicaSet",

  @doc("StatefulSet")
  statefulset: "StatefulSet",

  @doc("DaemonSet")
  daemonset: "DaemonSet",

  @doc("Job")
  job: "Job",

  @doc("CronJob")
  cronjob: "CronJob",

  @doc("Pod (standalone)")
  pod: "Pod",
}

@doc("Kubernetes pod phases")
enum K8sPodPhase {
  @doc("Pod is pending scheduling")
  pending: "Pending",

  @doc("Pod is running")
  running: "Running",

  @doc("Pod has succeeded (all containers exited 0)")
  succeeded: "Succeeded",

  @doc("Pod has failed (at least one container failed)")
  failed: "Failed",

  @doc("Pod phase is unknown")
  `unknown`: "Unknown",
}

@doc("Kubernetes container states")
enum K8sContainerState {
  @doc("Container is waiting to start")
  waiting: "waiting",

  @doc("Container is running")
  running: "running",

  @doc("Container has terminated")
  terminated: "terminated",
}

@doc("Kubernetes container waiting reasons")
enum K8sContainerWaitingReason {
  @doc("Container is being created")
  containerCreating: "ContainerCreating",

  @doc("Pulling image")
  pullingImage: "PullingImage",

  @doc("Image pull back-off")
  imagePullBackOff: "ImagePullBackOff",

  @doc("Error pulling image")
  errImagePull: "ErrImagePull",

  @doc("Crash loop back-off")
  crashLoopBackOff: "CrashLoopBackOff",

  @doc("Container config error")
  createContainerConfigError: "CreateContainerConfigError",

  @doc("Container creation error")
  createContainerError: "CreateContainerError",

  @doc("Pod initializing")
  podInitializing: "PodInitializing",
}

// =============================================================================
// Kubernetes Events
// =============================================================================

@doc("Kubernetes event")
model K8sEvent {
  @doc("Event type (Normal, Warning)")
  @encodedName("application/json", "k8s.event.type")
  eventType: K8sEventType;

  @doc("Event reason")
  @encodedName("application/json", "k8s.event.reason")
  reason: string;

  @doc("Event message")
  message: string;

  @doc("Event first timestamp")
  @encodedName("application/json", "first_timestamp")
  firstTimestamp: utcDateTime;

  @doc("Event last timestamp")
  @encodedName("application/json", "last_timestamp")
  lastTimestamp: utcDateTime;

  @doc("Event count")
  count: int32;

  @doc("Involved object kind")
  @encodedName("application/json", "involved_object.kind")
  involvedObjectKind: string;

  @doc("Involved object name")
  @encodedName("application/json", "involved_object.name")
  involvedObjectName: string;

  @doc("Involved object namespace")
  @encodedName("application/json", "involved_object.namespace")
  involvedObjectNamespace?: string;
}

@doc("Kubernetes event types")
enum K8sEventType {
  @doc("Normal event")
  normal: "Normal",

  @doc("Warning event")
  warning: "Warning",
}

// =============================================================================
// Kubernetes Metrics
// =============================================================================

@doc("Kubernetes pod CPU usage metric")
model K8sPodCpuMetric {
  @doc("Metric name")
  name: "k8s.pod.cpu.time";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Pod name")
  @encodedName("application/json", "k8s.pod.name")
  podName: string;

  @doc("Namespace")
  @encodedName("application/json", "k8s.namespace.name")
  namespaceName: string;
}

@doc("Kubernetes pod memory usage metric")
model K8sPodMemoryMetric {
  @doc("Metric name")
  name: "k8s.pod.memory.usage";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Pod name")
  @encodedName("application/json", "k8s.pod.name")
  podName: string;

  @doc("Namespace")
  @encodedName("application/json", "k8s.namespace.name")
  namespaceName: string;
}

@doc("Kubernetes deployment replicas metric")
model K8sDeploymentReplicasMetric {
  @doc("Metric name")
  name: "k8s.deployment.replicas";

  @doc("Metric unit")
  unit: "{replica}";

  @doc("Deployment name")
  @encodedName("application/json", "k8s.deployment.name")
  deploymentName: string;

  @doc("Namespace")
  @encodedName("application/json", "k8s.namespace.name")
  namespaceName: string;

  @doc("Replica state")
  @encodedName("application/json", "k8s.deployment.replica.state")
  replicaState: K8sReplicaState;
}

@doc("Kubernetes replica states")
enum K8sReplicaState {
  @doc("Desired replicas")
  desired: "desired",

  @doc("Available replicas")
  available: "available",

  @doc("Ready replicas")
  ready: "ready",

  @doc("Updated replicas")
  updated: "updated",

  @doc("Unavailable replicas")
  unavailable: "unavailable",
}
