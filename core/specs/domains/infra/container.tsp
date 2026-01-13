// =============================================================================
// QYL v2.0 - Container Semantic Conventions (OTel v1.38)
// =============================================================================
// Container resource and runtime attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Infra.Container;

// =============================================================================
// Container Resource Attributes
// =============================================================================

@doc("Container resource attributes")
model ContainerAttributes {
  @doc("Container ID (full 64-character hex)")
  @encodedName("application/json", "container.id")
  id?: string;

  @doc("Container name")
  @encodedName("application/json", "container.name")
  name?: string;

  @doc("Container runtime (e.g., docker, containerd, cri-o)")
  @encodedName("application/json", "container.runtime")
  runtime?: ContainerRuntime;

  @doc("Container image name")
  @encodedName("application/json", "container.image.name")
  imageName?: string;

  @doc("Container image ID")
  @encodedName("application/json", "container.image.id")
  imageId?: string;

  @doc("Container image tag")
  @encodedName("application/json", "container.image.tag")
  imageTag?: string;

  @doc("Container image digest")
  @encodedName("application/json", "container.image.repo_digests")
  imageRepoDigests?: string[];

  @doc("Container command")
  @encodedName("application/json", "container.command")
  command?: string;

  @doc("Container command line")
  @encodedName("application/json", "container.command_line")
  commandLine?: string;

  @doc("Container command arguments")
  @encodedName("application/json", "container.command_args")
  commandArgs?: string[];

  @doc("Container labels. Keys map to container.label.<key>")
  labels?: Record<string>;
}

// =============================================================================
// Container Runtime Enumerations
// =============================================================================

@doc("Container runtimes")
enum ContainerRuntime {
  @doc("Docker runtime")
  docker: "docker",

  @doc("containerd runtime")
  containerd: "containerd",

  @doc("CRI-O runtime")
  crio: "cri-o",

  @doc("rkt runtime (deprecated)")
  rkt: "rkt",

  @doc("LXC runtime")
  lxc: "lxc",

  @doc("Podman runtime")
  podman: "podman",

  @doc("gVisor runtime")
  gvisor: "gvisor",

  @doc("Kata Containers")
  kata: "kata",

  @doc("Firecracker")
  firecracker: "firecracker",
}

@doc("Container states")
enum ContainerState {
  @doc("Container is being created")
  creating: "creating",

  @doc("Container is running")
  running: "running",

  @doc("Container is paused")
  paused: "paused",

  @doc("Container is restarting")
  restarting: "restarting",

  @doc("Container has exited")
  exited: "exited",

  @doc("Container is being removed")
  removing: "removing",

  @doc("Container is dead")
  dead: "dead",
}

// =============================================================================
// Container Metrics
// =============================================================================

@doc("Container CPU usage metric")
model ContainerCpuUsageMetric {
  @doc("Metric name")
  name: "container.cpu.time";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Container ID")
  @encodedName("application/json", "container.id")
  containerId: string;

  @doc("Container name")
  @encodedName("application/json", "container.name")
  containerName?: string;

  @doc("CPU state")
  @encodedName("application/json", "system.cpu.state")
  cpuState?: string;
}

@doc("Container memory usage metric")
model ContainerMemoryUsageMetric {
  @doc("Metric name")
  name: "container.memory.usage";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Container ID")
  @encodedName("application/json", "container.id")
  containerId: string;

  @doc("Container name")
  @encodedName("application/json", "container.name")
  containerName?: string;
}

@doc("Container network IO metric")
model ContainerNetworkIoMetric {
  @doc("Metric name")
  name: "container.network.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Container ID")
  @encodedName("application/json", "container.id")
  containerId: string;

  @doc("Network interface")
  @encodedName("application/json", "network.interface")
  networkInterface: string;

  @doc("IO direction")
  @encodedName("application/json", "network.direction")
  direction: string;
}

@doc("Container disk IO metric")
model ContainerDiskIoMetric {
  @doc("Metric name")
  name: "container.disk.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Container ID")
  @encodedName("application/json", "container.id")
  containerId: string;

  @doc("Disk device")
  @encodedName("application/json", "disk.device")
  diskDevice: string;

  @doc("IO direction")
  @encodedName("application/json", "disk.direction")
  direction: string;
}
