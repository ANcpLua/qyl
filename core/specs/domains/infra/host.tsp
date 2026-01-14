// =============================================================================
// QYL v2.0 - Host Semantic Conventions (OTel v1.39)
// =============================================================================
// Host/machine resource attributes.
// =============================================================================

import "../../common/types.tsp";
import "../../otel/resource.tsp";

using Qyl.Common;
using Qyl.OTel.Resource;

namespace Qyl.Domains.Infra.Host;

// =============================================================================
// Host Resource Attributes
// =============================================================================

@doc("Host resource attributes")
model HostAttributes {
  @doc("Unique host identifier")
  @encodedName("application/json", "host.id")
  id?: string;

  @doc("Hostname")
  @encodedName("application/json", "host.name")
  name?: string;

  @doc("Host type (e.g., n1-standard-1, m5.large)")
  @encodedName("application/json", "host.type")
  hostType?: string;

  @doc("Host architecture")
  @encodedName("application/json", "host.arch")
  arch?: HostArch;

  @doc("Host image name")
  @encodedName("application/json", "host.image.name")
  imageName?: string;

  @doc("Host image ID")
  @encodedName("application/json", "host.image.id")
  imageId?: string;

  @doc("Host image version")
  @encodedName("application/json", "host.image.version")
  imageVersion?: string;

  @doc("Host IP addresses")
  @encodedName("application/json", "host.ip")
  ip?: IpAddress[];

  @doc("Host MAC addresses")
  @encodedName("application/json", "host.mac")
  mac?: MacAddress[];

  @doc("Number of CPU cores")
  @encodedName("application/json", "host.cpu.count")
  cpuCount?: int32;

  @doc("CPU model name")
  @encodedName("application/json", "host.cpu.model.name")
  cpuModelName?: string;

  @doc("CPU vendor")
  @encodedName("application/json", "host.cpu.vendor.id")
  cpuVendorId?: string;

  @doc("CPU cache size in bytes")
  @encodedName("application/json", "host.cpu.cache.size")
  cpuCacheSize?: ByteSize;

  @doc("CPU stepping")
  @encodedName("application/json", "host.cpu.stepping")
  cpuStepping?: string;

  @doc("CPU family")
  @encodedName("application/json", "host.cpu.family")
  cpuFamily?: string;
}

// =============================================================================
// Host Metrics
// =============================================================================

@doc("Host CPU utilization metric")
model HostCpuUtilizationMetric {
  @doc("Metric name")
  name: "system.cpu.utilization";

  @doc("Metric unit (ratio)")
  unit: "1";

  @doc("CPU state (user, system, idle, etc.)")
  @encodedName("application/json", "system.cpu.state")
  cpuState: CpuState;

  @doc("CPU logical number")
  @encodedName("application/json", "system.cpu.logical_number")
  cpuLogicalNumber?: int32;
}

@doc("Host memory usage metric")
model HostMemoryUsageMetric {
  @doc("Metric name")
  name: "system.memory.usage";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Memory state")
  @encodedName("application/json", "system.memory.state")
  memoryState: MemoryState;
}

@doc("Host disk IO metric")
model HostDiskIoMetric {
  @doc("Metric name")
  name: "system.disk.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Disk device name")
  @encodedName("application/json", "system.disk.device")
  diskDevice: string;

  @doc("IO direction")
  @encodedName("application/json", "system.disk.direction")
  diskDirection: DiskDirection;
}

@doc("Host network IO metric")
model HostNetworkIoMetric {
  @doc("Metric name")
  name: "system.network.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Network interface device")
  @encodedName("application/json", "system.network.device")
  networkDevice: string;

  @doc("IO direction")
  @encodedName("application/json", "system.network.direction")
  networkDirection: NetworkDirection;
}

// =============================================================================
// Host Enumerations
// =============================================================================

@doc("CPU states")
enum CpuState {
  @doc("User mode")
  user: "user",

  @doc("System/kernel mode")
  system: "system",

  @doc("Idle")
  idle: "idle",

  @doc("I/O wait")
  iowait: "iowait",

  @doc("Interrupt handling")
  interrupt: "interrupt",

  @doc("Nice (low priority user mode)")
  nice: "nice",

  @doc("Steal (virtualization overhead)")
  steal: "steal",

  @doc("Soft interrupt handling")
  softirq: "softirq",
}

@doc("Memory states")
enum MemoryState {
  @doc("Used memory")
  used: "used",

  @doc("Free memory")
  free: "free",

  @doc("Buffered memory")
  buffered: "buffered",

  @doc("Cached memory")
  cached: "cached",

  @doc("Shared memory")
  shared: "shared",

  @doc("Slab memory")
  slab: "slab",
}

@doc("Disk IO directions")
enum DiskDirection {
  @doc("Read operations")
  read: "read",

  @doc("Write operations")
  write: "write",
}

@doc("Network IO directions")
enum NetworkDirection {
  @doc("Transmit/send")
  transmit: "transmit",

  @doc("Receive")
  receive: "receive",
}
