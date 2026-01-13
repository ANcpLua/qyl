// =============================================================================
// QYL v2.0 - .NET Runtime Semantic Conventions (OTel v1.38)
// =============================================================================
// .NET-specific runtime telemetry attributes and metrics.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Runtime.DotNet;

// =============================================================================
// .NET Runtime Attributes
// =============================================================================

@doc(".NET runtime attributes")
model DotNetAttributes {
  @doc("CLR version")
  @encodedName("application/json", "dotnet.clr.version")
  clrVersion?: SemVer;

  @doc(".NET runtime identifier (e.g., net8.0)")
  @encodedName("application/json", "dotnet.runtime.identifier")
  runtimeIdentifier?: string;

  @doc("Is server GC enabled")
  @encodedName("application/json", "dotnet.gc.server")
  gcServer?: boolean;

  @doc("Is concurrent GC enabled")
  @encodedName("application/json", "dotnet.gc.concurrent")
  gcConcurrent?: boolean;
}

// =============================================================================
// .NET GC Metrics
// =============================================================================

@doc(".NET GC heap size metric")
model DotNetGcHeapSizeMetric {
  @doc("Metric name")
  name: "dotnet.gc.heap.size";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("GC generation")
  @encodedName("application/json", "dotnet.gc.generation")
  generation: DotNetGcGeneration;
}

@doc(".NET GC heap fragmentation metric")
model DotNetGcFragmentationMetric {
  @doc("Metric name")
  name: "dotnet.gc.heap.fragmentation.size";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("GC generation")
  @encodedName("application/json", "dotnet.gc.generation")
  generation: DotNetGcGeneration;
}

@doc(".NET GC collections metric")
model DotNetGcCollectionsMetric {
  @doc("Metric name")
  name: "dotnet.gc.collections";

  @doc("Metric unit")
  unit: "{collection}";

  @doc("GC generation")
  @encodedName("application/json", "dotnet.gc.generation")
  generation: DotNetGcGeneration;
}

@doc(".NET GC duration metric")
model DotNetGcDurationMetric {
  @doc("Metric name")
  name: "dotnet.gc.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("GC generation")
  @encodedName("application/json", "dotnet.gc.generation")
  generation: DotNetGcGeneration;
}

@doc(".NET GC allocated bytes metric")
model DotNetGcAllocatedMetric {
  @doc("Metric name")
  name: "dotnet.gc.allocated_bytes";

  @doc("Metric unit (bytes)")
  unit: "By";
}

@doc(".NET GC committed bytes metric")
model DotNetGcCommittedMetric {
  @doc("Metric name")
  name: "dotnet.gc.committed_memory.size";

  @doc("Metric unit (bytes)")
  unit: "By";
}

@doc(".NET GC pause time metric")
model DotNetGcPauseTimeMetric {
  @doc("Metric name")
  name: "dotnet.gc.pause.time";

  @doc("Metric unit (seconds)")
  unit: "s";
}

// =============================================================================
// .NET JIT Metrics
// =============================================================================

@doc(".NET JIT compilation time metric")
model DotNetJitCompilationTimeMetric {
  @doc("Metric name")
  name: "dotnet.jit.compilation.time";

  @doc("Metric unit (seconds)")
  unit: "s";
}

@doc(".NET JIT compiled methods metric")
model DotNetJitCompiledMethodsMetric {
  @doc("Metric name")
  name: "dotnet.jit.compiled_methods";

  @doc("Metric unit")
  unit: "{method}";
}

@doc(".NET JIT compiled IL bytes metric")
model DotNetJitCompiledIlBytesMetric {
  @doc("Metric name")
  name: "dotnet.jit.compiled_il_bytes";

  @doc("Metric unit (bytes)")
  unit: "By";
}

// =============================================================================
// .NET Thread Pool Metrics
// =============================================================================

@doc(".NET thread pool thread count metric")
model DotNetThreadPoolThreadCountMetric {
  @doc("Metric name")
  name: "dotnet.thread_pool.thread.count";

  @doc("Metric unit")
  unit: "{thread}";
}

@doc(".NET thread pool queue length metric")
model DotNetThreadPoolQueueLengthMetric {
  @doc("Metric name")
  name: "dotnet.thread_pool.queue.length";

  @doc("Metric unit")
  unit: "{item}";
}

@doc(".NET thread pool completed items metric")
model DotNetThreadPoolCompletedItemsMetric {
  @doc("Metric name")
  name: "dotnet.thread_pool.completed_items.count";

  @doc("Metric unit")
  unit: "{item}";
}

// =============================================================================
// .NET Monitor/Lock Metrics
// =============================================================================

@doc(".NET monitor lock contention metric")
model DotNetMonitorLockContentionMetric {
  @doc("Metric name")
  name: "dotnet.monitor.lock_contention.count";

  @doc("Metric unit")
  unit: "{contention}";
}

// =============================================================================
// .NET Timer Metrics
// =============================================================================

@doc(".NET timer count metric")
model DotNetTimerCountMetric {
  @doc("Metric name")
  name: "dotnet.timer.count";

  @doc("Metric unit")
  unit: "{timer}";
}

// =============================================================================
// .NET Assembly Metrics
// =============================================================================

@doc(".NET assembly count metric")
model DotNetAssemblyCountMetric {
  @doc("Metric name")
  name: "dotnet.assembly.count";

  @doc("Metric unit")
  unit: "{assembly}";
}

// =============================================================================
// .NET Exception Metrics
// =============================================================================

@doc(".NET exception count metric")
model DotNetExceptionCountMetric {
  @doc("Metric name")
  name: "dotnet.exceptions";

  @doc("Metric unit")
  unit: "{exception}";

  @doc("Exception type")
  @encodedName("application/json", "exception.type")
  exceptionType?: string;
}

// =============================================================================
// .NET Enumerations
// =============================================================================

@doc(".NET GC generations")
enum DotNetGcGeneration {
  @doc("Generation 0 (short-lived)")
  gen0: "gen0",

  @doc("Generation 1 (medium-lived)")
  gen1: "gen1",

  @doc("Generation 2 (long-lived)")
  gen2: "gen2",

  @doc("Large object heap")
  loh: "loh",

  @doc("Pinned object heap")
  poh: "poh",
}

@doc(".NET GC types")
enum DotNetGcType {
  @doc("Background GC")
  background: "background",

  @doc("Blocking GC")
  blocking: "blocking",

  @doc("Ephemeral GC")
  ephemeral: "ephemeral",

  @doc("Full blocking GC")
  fullBlocking: "full_blocking",
}

@doc(".NET GC compaction reasons")
enum DotNetGcCompactionReason {
  @doc("Low memory")
  lowMemory: "low_memory",

  @doc("Fragmentation")
  fragmentation: "fragmentation",

  @doc("Induced")
  induced: "induced",

  @doc("Large object heap allocation")
  lohAllocation: "loh_allocation",
}
