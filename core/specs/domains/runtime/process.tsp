// =============================================================================
// QYL v2.0 - Process Semantic Conventions (OTel v1.39)
// =============================================================================
// Operating system process telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Runtime.Process;

// =============================================================================
// Process Resource Attributes
// =============================================================================

@doc("Process resource attributes")
model ProcessAttributes {
  @doc("Process ID (PID)")
  @encodedName("application/json", "process.pid")
  pid?: int64;

  @doc("Parent process ID (PPID)")
  @encodedName("application/json", "process.parent_pid")
  parentPid?: int64;

  @doc("Executable name")
  @encodedName("application/json", "process.executable.name")
  executableName?: string;

  @doc("Executable path")
  @encodedName("application/json", "process.executable.path")
  executablePath?: string;

  @doc("Command used to launch the process")
  @encodedName("application/json", "process.command")
  command?: string;

  @doc("Full command line (including args)")
  @encodedName("application/json", "process.command_line")
  commandLine?: string;

  @doc("Command arguments")
  @encodedName("application/json", "process.command_args")
  commandArgs?: string[];

  @doc("User running the process")
  @encodedName("application/json", "process.owner")
  owner?: string;

  @doc("User ID running the process")
  @encodedName("application/json", "process.user.id")
  userId?: string;

  @doc("User name running the process")
  @encodedName("application/json", "process.user.name")
  userName?: string;

  @doc("Group ID")
  @encodedName("application/json", "process.group.id")
  groupId?: string;

  @doc("Group name")
  @encodedName("application/json", "process.group.name")
  groupName?: string;

  @doc("Process creation time")
  @encodedName("application/json", "process.creation.time")
  creationTime?: utcDateTime;

  @doc("Process working directory")
  @encodedName("application/json", "process.working_directory")
  workingDirectory?: string;

  // ---------------------------------------------------------------------------
  // Runtime Attributes
  // ---------------------------------------------------------------------------

  @doc("Runtime name (e.g., .NET, JVM, Python)")
  @encodedName("application/json", "process.runtime.name")
  runtimeName?: string;

  @doc("Runtime version")
  @encodedName("application/json", "process.runtime.version")
  runtimeVersion?: string;

  @doc("Runtime description")
  @encodedName("application/json", "process.runtime.description")
  runtimeDescription?: string;

  // ---------------------------------------------------------------------------
  // Context Switch Attributes
  // ---------------------------------------------------------------------------

  @doc("Voluntary context switches")
  @encodedName("application/json", "process.context_switches.voluntary")
  contextSwitchesVoluntary?: Count;

  @doc("Involuntary context switches")
  @encodedName("application/json", "process.context_switches.involuntary")
  contextSwitchesInvoluntary?: Count;

  // ---------------------------------------------------------------------------
  // Paging Attributes
  // ---------------------------------------------------------------------------

  @doc("Major page faults")
  @encodedName("application/json", "process.paging.major_faults")
  pagingMajorFaults?: Count;

  @doc("Minor page faults")
  @encodedName("application/json", "process.paging.minor_faults")
  pagingMinorFaults?: Count;

  // ---------------------------------------------------------------------------
  // Environment Variables (Template Attribute)
  // ---------------------------------------------------------------------------

  @doc("Environment variables. Keys map to process.environment_variable.<key>")
  @encodedName("application/json", "process.environment_variable")
  environmentVariables?: Record<string>;
}

// =============================================================================
// Process Metrics
// =============================================================================

@doc("Process CPU time metric")
model ProcessCpuTimeMetric {
  @doc("Metric name")
  name: "process.cpu.time";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("CPU state")
  @encodedName("application/json", "process.cpu.state")
  cpuState: ProcessCpuState;
}

@doc("Process CPU utilization metric")
model ProcessCpuUtilizationMetric {
  @doc("Metric name")
  name: "process.cpu.utilization";

  @doc("Metric unit (ratio)")
  unit: "1";

  @doc("CPU state")
  @encodedName("application/json", "process.cpu.state")
  cpuState: ProcessCpuState;
}

@doc("Process memory usage metric")
model ProcessMemoryUsageMetric {
  @doc("Metric name")
  name: "process.memory.usage";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Memory type")
  @encodedName("application/json", "process.memory.type")
  memoryType: ProcessMemoryType;
}

@doc("Process memory virtual metric")
model ProcessMemoryVirtualMetric {
  @doc("Metric name")
  name: "process.memory.virtual";

  @doc("Metric unit (bytes)")
  unit: "By";
}

@doc("Process disk IO metric")
model ProcessDiskIoMetric {
  @doc("Metric name")
  name: "process.disk.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("IO direction")
  @encodedName("application/json", "disk.io.direction")
  direction: string;
}

@doc("Process network IO metric")
model ProcessNetworkIoMetric {
  @doc("Metric name")
  name: "process.network.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("IO direction")
  @encodedName("application/json", "network.io.direction")
  direction: string;
}

@doc("Process thread count metric")
model ProcessThreadCountMetric {
  @doc("Metric name")
  name: "process.thread.count";

  @doc("Metric unit")
  unit: "{thread}";
}

@doc("Process open file descriptors metric")
model ProcessOpenFilesMetric {
  @doc("Metric name")
  name: "process.open_file_descriptor.count";

  @doc("Metric unit")
  unit: "{file_descriptor}";
}

// =============================================================================
// Process Enumerations
// =============================================================================

@doc("Process CPU states")
enum ProcessCpuState {
  @doc("User mode CPU time")
  user: "user",

  @doc("System/kernel mode CPU time")
  system: "system",

  @doc("Wait state")
  wait: "wait",
}

@doc("Process memory types")
enum ProcessMemoryType {
  @doc("Physical/resident memory")
  physical: "physical",

  @doc("Virtual memory")
  virtual: "virtual",
}
