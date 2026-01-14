// =============================================================================
// QYL v2.0 - Thread Semantic Conventions (OTel v1.39)
// =============================================================================
// Thread telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Runtime.Thread;

// =============================================================================
// Thread Attributes
// =============================================================================

@doc("Thread attributes")
model ThreadAttributes {
  @doc("Thread ID")
  @encodedName("application/json", "thread.id")
  id?: int64;

  @doc("Thread name")
  @encodedName("application/json", "thread.name")
  name?: string;

  @doc("Thread daemon status")
  @encodedName("application/json", "thread.daemon")
  daemon?: boolean;
}

// =============================================================================
// Thread Pool Metrics
// =============================================================================

@doc("Thread pool active threads metric")
model ThreadPoolActiveMetric {
  @doc("Metric name")
  name: "thread_pool.active_threads";

  @doc("Metric unit")
  unit: "{thread}";

  @doc("Thread pool name")
  @encodedName("application/json", "thread_pool.name")
  poolName: string;
}

@doc("Thread pool queue length metric")
model ThreadPoolQueueLengthMetric {
  @doc("Metric name")
  name: "thread_pool.queue.length";

  @doc("Metric unit")
  unit: "{task}";

  @doc("Thread pool name")
  @encodedName("application/json", "thread_pool.name")
  poolName: string;
}

@doc("Thread pool size metric")
model ThreadPoolSizeMetric {
  @doc("Metric name")
  name: "thread_pool.threads";

  @doc("Metric unit")
  unit: "{thread}";

  @doc("Thread pool name")
  @encodedName("application/json", "thread_pool.name")
  poolName: string;

  @doc("Thread state")
  @encodedName("application/json", "thread_pool.thread.state")
  threadState: ThreadPoolState;
}

@doc("Thread pool completed tasks metric")
model ThreadPoolCompletedMetric {
  @doc("Metric name")
  name: "thread_pool.completed_tasks";

  @doc("Metric unit")
  unit: "{task}";

  @doc("Thread pool name")
  @encodedName("application/json", "thread_pool.name")
  poolName: string;
}

// =============================================================================
// Thread Enumerations
// =============================================================================

@doc("Thread pool states")
enum ThreadPoolState {
  @doc("Active threads")
  active: "active",

  @doc("Idle threads")
  idle: "idle",

  @doc("Core threads")
  core: "core",

  @doc("Maximum threads")
  max: "max",
}
