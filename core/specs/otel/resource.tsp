// =============================================================================
// QYL v2.0 - OpenTelemetry Resource Model
// =============================================================================
// Resource attributes identifying the entity producing telemetry.
// =============================================================================

import "../common/types.tsp";
import "./enums.tsp";

using Qyl.Common;

namespace Qyl.OTel.Resource;

// =============================================================================
// Resource Model
// =============================================================================

@doc("Resource describes the entity producing telemetry")
model Resource {
  @doc("Service name (required)")
  @encodedName("application/json", "service.name")
  serviceName: string;

  @doc("Service namespace for grouping")
  @encodedName("application/json", "service.namespace")
  serviceNamespace?: string;

  @doc("Service instance ID (unique per instance)")
  @encodedName("application/json", "service.instance.id")
  serviceInstanceId?: string;

  @doc("Service version")
  @encodedName("application/json", "service.version")
  serviceVersion?: SemVer;

  @doc("Telemetry SDK name")
  @encodedName("application/json", "telemetry.sdk.name")
  telemetrySdkName?: string;

  @doc("Telemetry SDK language")
  @encodedName("application/json", "telemetry.sdk.language")
  telemetrySdkLanguage?: Enums.TelemetrySdkLanguage;

  @doc("Telemetry SDK version")
  @encodedName("application/json", "telemetry.sdk.version")
  telemetrySdkVersion?: SemVer;

  @doc("Auto-instrumentation agent name")
  @encodedName("application/json", "telemetry.auto.version")
  telemetryAutoVersion?: SemVer;

  @doc("Deployment environment (e.g., production, staging)")
  @encodedName("application/json", "deployment.environment.name")
  deploymentEnvironment?: string;

  @doc("Cloud provider")
  @encodedName("application/json", "cloud.provider")
  cloudProvider?: CloudProvider;

  @doc("Cloud region")
  @encodedName("application/json", "cloud.region")
  cloudRegion?: string;

  @doc("Cloud availability zone")
  @encodedName("application/json", "cloud.availability_zone")
  cloudAvailabilityZone?: string;

  @doc("Cloud account ID")
  @encodedName("application/json", "cloud.account.id")
  cloudAccountId?: string;

  @doc("Cloud platform (e.g., aws_ecs, gcp_cloud_run)")
  @encodedName("application/json", "cloud.platform")
  cloudPlatform?: string;

  @doc("Host name")
  @encodedName("application/json", "host.name")
  hostName?: string;

  @doc("Host ID")
  @encodedName("application/json", "host.id")
  hostId?: string;

  @doc("Host type (e.g., n1-standard-1)")
  @encodedName("application/json", "host.type")
  hostType?: string;

  @doc("Host architecture (e.g., amd64, arm64)")
  @encodedName("application/json", "host.arch")
  hostArch?: HostArch;

  @doc("Operating system type")
  @encodedName("application/json", "os.type")
  osType?: OsType;

  @doc("Operating system description")
  @encodedName("application/json", "os.description")
  osDescription?: string;

  @doc("Operating system version")
  @encodedName("application/json", "os.version")
  osVersion?: string;

  @doc("Process ID")
  @encodedName("application/json", "process.pid")
  processPid?: int64;

  @doc("Process executable name")
  @encodedName("application/json", "process.executable.name")
  processExecutableName?: string;

  @doc("Process command line")
  @encodedName("application/json", "process.command_line")
  processCommandLine?: string;

  @doc("Process runtime name")
  @encodedName("application/json", "process.runtime.name")
  processRuntimeName?: string;

  @doc("Process runtime version")
  @encodedName("application/json", "process.runtime.version")
  processRuntimeVersion?: string;

  @doc("Container ID")
  @encodedName("application/json", "container.id")
  containerId?: string;

  @doc("Container name")
  @encodedName("application/json", "container.name")
  containerName?: string;

  @doc("Container image name")
  @encodedName("application/json", "container.image.name")
  containerImageName?: string;

  @doc("Container image tag")
  @encodedName("application/json", "container.image.tag")
  containerImageTag?: string;

  @doc("Kubernetes cluster name")
  @encodedName("application/json", "k8s.cluster.name")
  k8sClusterName?: string;

  @doc("Kubernetes namespace")
  @encodedName("application/json", "k8s.namespace.name")
  k8sNamespaceName?: string;

  @doc("Kubernetes pod name")
  @encodedName("application/json", "k8s.pod.name")
  k8sPodName?: string;

  @doc("Kubernetes pod UID")
  @encodedName("application/json", "k8s.pod.uid")
  k8sPodUid?: string;

  @doc("Kubernetes deployment name")
  @encodedName("application/json", "k8s.deployment.name")
  k8sDeploymentName?: string;

  @doc("Additional resource attributes")
  attributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: Count;
}

// =============================================================================
// Resource-Related Enums
// =============================================================================

@doc("Cloud provider types")
enum CloudProvider {
  @doc("Alibaba Cloud")
  alibabaCloud: "alibaba_cloud",

  @doc("Amazon Web Services")
  aws: "aws",

  @doc("Microsoft Azure")
  azure: "azure",

  @doc("Google Cloud Platform")
  gcp: "gcp",

  @doc("Heroku")
  heroku: "heroku",

  @doc("IBM Cloud")
  ibmCloud: "ibm_cloud",

  @doc("Tencent Cloud")
  tencentCloud: "tencent_cloud",
}

@doc("Host architecture types")
enum HostArch {
  @doc("AMD64 / x86_64")
  amd64: "amd64",

  @doc("ARM32")
  arm32: "arm32",

  @doc("ARM64 / aarch64")
  arm64: "arm64",

  @doc("Itanium")
  ia64: "ia64",

  @doc("PowerPC 32-bit")
  ppc32: "ppc32",

  @doc("PowerPC 64-bit")
  ppc64: "ppc64",

  @doc("IBM z/Architecture")
  s390x: "s390x",

  @doc("x86 32-bit")
  x86: "x86",
}

@doc("Operating system types")
enum OsType {
  @doc("Microsoft Windows")
  windows: "windows",

  @doc("Linux")
  linux: "linux",

  @doc("Apple macOS")
  darwin: "darwin",

  @doc("FreeBSD")
  freebsd: "freebsd",

  @doc("NetBSD")
  netbsd: "netbsd",

  @doc("OpenBSD")
  openbsd: "openbsd",

  @doc("DragonFly BSD")
  dragonflybsd: "dragonflybsd",

  @doc("HP-UX")
  hpux: "hpux",

  @doc("AIX")
  aix: "aix",

  @doc("Oracle Solaris")
  solaris: "solaris",

  @doc("IBM z/OS")
  zOs: "z_os",
}
