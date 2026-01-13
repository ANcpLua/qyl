// =============================================================================
// QYL v2.0 - FaaS Semantic Conventions (OTel v1.38)
// =============================================================================
// Function-as-a-Service telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Infra.FaaS;

// =============================================================================
// FaaS Span Attributes
// =============================================================================

@doc("FaaS span attributes")
model FaasAttributes {
  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name?: string;

  @doc("Function version")
  @encodedName("application/json", "faas.version")
  version?: string;

  @doc("Cloud resource ID of the function")
  @encodedName("application/json", "cloud.resource_id")
  cloudResourceId?: string;

  @doc("Function invocation ID")
  @encodedName("application/json", "faas.invocation_id")
  invocationId?: string;

  @doc("Trigger type")
  @encodedName("application/json", "faas.trigger")
  trigger?: FaasTrigger;

  @doc("Whether this is a cold start")
  @encodedName("application/json", "faas.coldstart")
  coldstart?: boolean;

  @doc("Maximum memory allocated (in bytes)")
  @encodedName("application/json", "faas.max_memory")
  maxMemory?: ByteSize;

  @doc("Execution instance ID")
  @encodedName("application/json", "faas.instance")
  instance?: string;

  // ---------------------------------------------------------------------------
  // Trigger-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("Document name (for datasource triggers)")
  @encodedName("application/json", "faas.document.name")
  documentName?: string;

  @doc("Document collection (for datasource triggers)")
  @encodedName("application/json", "faas.document.collection")
  documentCollection?: string;

  @doc("Document operation (for datasource triggers)")
  @encodedName("application/json", "faas.document.operation")
  documentOperation?: FaasDocumentOperation;

  @doc("Time (for timer triggers)")
  @encodedName("application/json", "faas.time")
  time?: utcDateTime;

  @doc("Cron schedule (for timer triggers)")
  @encodedName("application/json", "faas.cron")
  cron?: string;

  // ---------------------------------------------------------------------------
  // Invoked Function Attributes (for client spans)
  // ---------------------------------------------------------------------------

  @doc("Invoked function name")
  @encodedName("application/json", "faas.invoked_name")
  invokedName?: string;

  @doc("Invoked function provider")
  @encodedName("application/json", "faas.invoked_provider")
  invokedProvider?: string;

  @doc("Invoked function region")
  @encodedName("application/json", "faas.invoked_region")
  invokedRegion?: string;
}

// =============================================================================
// FaaS Enumerations
// =============================================================================

@doc("FaaS trigger types")
enum FaasTrigger {
  @doc("HTTP/API Gateway trigger")
  http: "http",

  @doc("Pub/Sub or queue message trigger")
  pubsub: "pubsub",

  @doc("Database/document change trigger")
  datasource: "datasource",

  @doc("Timer/scheduled trigger")
  timer: "timer",

  @doc("Direct invocation trigger")
  other: "other",
}

@doc("FaaS document operations")
enum FaasDocumentOperation {
  @doc("Document insert")
  insert: "insert",

  @doc("Document edit/update")
  edit: "edit",

  @doc("Document delete")
  delete: "delete",
}

// =============================================================================
// FaaS Metrics
// =============================================================================

@doc("FaaS invocation count metric")
model FaasInvocationMetric {
  @doc("Metric name")
  name: "faas.invocations";

  @doc("Metric unit")
  unit: "{invocation}";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;

  @doc("Trigger type")
  @encodedName("application/json", "faas.trigger")
  trigger: FaasTrigger;
}

@doc("FaaS invocation duration metric")
model FaasInvocationDurationMetric {
  @doc("Metric name")
  name: "faas.invoke_duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;

  @doc("Trigger type")
  @encodedName("application/json", "faas.trigger")
  trigger: FaasTrigger;

  @doc("Error type if failed")
  @encodedName("application/json", "error.type")
  errorType?: string;
}

@doc("FaaS cold start count metric")
model FaasColdStartMetric {
  @doc("Metric name")
  name: "faas.coldstarts";

  @doc("Metric unit")
  unit: "{coldstart}";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;

  @doc("Trigger type")
  @encodedName("application/json", "faas.trigger")
  trigger: FaasTrigger;
}

@doc("FaaS init duration metric")
model FaasInitDurationMetric {
  @doc("Metric name")
  name: "faas.init_duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;

  @doc("Trigger type")
  @encodedName("application/json", "faas.trigger")
  trigger: FaasTrigger;
}

@doc("FaaS memory usage metric")
model FaasMemoryUsageMetric {
  @doc("Metric name")
  name: "faas.mem_usage";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;
}

@doc("FaaS CPU usage metric")
model FaasCpuUsageMetric {
  @doc("Metric name")
  name: "faas.cpu_usage";

  @doc("Metric unit (ratio)")
  unit: "1";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;
}

@doc("FaaS concurrency metric")
model FaasConcurrencyMetric {
  @doc("Metric name")
  name: "faas.active_instances";

  @doc("Metric unit")
  unit: "{instance}";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;
}

@doc("FaaS timeout metric")
model FaasTimeoutMetric {
  @doc("Metric name")
  name: "faas.timeouts";

  @doc("Metric unit")
  unit: "{timeout}";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;

  @doc("Trigger type")
  @encodedName("application/json", "faas.trigger")
  trigger: FaasTrigger;
}

@doc("FaaS errors metric")
model FaasErrorsMetric {
  @doc("Metric name")
  name: "faas.errors";

  @doc("Metric unit")
  unit: "{error}";

  @doc("Function name")
  @encodedName("application/json", "faas.name")
  name_attr: string;

  @doc("Trigger type")
  @encodedName("application/json", "faas.trigger")
  trigger: FaasTrigger;

  @doc("Error type")
  @encodedName("application/json", "error.type")
  errorType?: string;
}
