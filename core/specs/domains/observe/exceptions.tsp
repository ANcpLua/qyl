// =============================================================================
// QYL v2.0 - Exception Semantic Conventions (OTel v1.38)
// =============================================================================
// Exception/error telemetry attributes.
// =============================================================================

import "../../common/types.tsp";
import "../ai/code.tsp";

using Qyl.Common;
using Qyl.Domains.AI.Code;

namespace Qyl.Domains.Observe.Exceptions;

// =============================================================================
// Exception Span Event Attributes
// =============================================================================

@doc("Exception span event attributes")
model ExceptionAttributes {
  @doc("Exception type/class name")
  @encodedName("application/json", "exception.type")
  exceptionType: string;

  @doc("Exception message")
  @encodedName("application/json", "exception.message")
  message?: string;

  @doc("Exception stacktrace")
  @encodedName("application/json", "exception.stacktrace")
  stacktrace?: string;

  @doc("Whether the exception escaped the span scope")
  @encodedName("application/json", "exception.escaped")
  escaped?: boolean;

  @doc("Exception ID (for deduplication)")
  @encodedName("application/json", "exception.id")
  id?: string;
}

// =============================================================================
// Exception Event Model
// =============================================================================

@doc("Exception event following OTel spec")
model ExceptionEvent {
  @doc("Event name (always 'exception')")
  @encodedName("application/json", "event.name")
  eventName: "exception";

  @doc("Exception type/class name")
  @encodedName("application/json", "exception.type")
  exceptionType: string;

  @doc("Exception message")
  @encodedName("application/json", "exception.message")
  message: string;

  @doc("Exception stacktrace")
  @encodedName("application/json", "exception.stacktrace")
  stacktrace?: string;

  @doc("Whether the exception escaped")
  @encodedName("application/json", "exception.escaped")
  escaped: boolean;

  @doc("Event timestamp")
  timestamp: utcDateTime;

  @doc("Associated trace ID")
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Associated span ID")
  @encodedName("application/json", "span_id")
  spanId?: SpanId;
}

// =============================================================================
// Enriched Exception Model
// =============================================================================

@doc("Enriched exception with parsed stack trace")
model EnrichedException {
  @doc("Exception type/class name")
  @encodedName("application/json", "exception_type")
  exceptionType: string;

  @doc("Exception message")
  message: string;

  @doc("Parsed stack trace")
  @encodedName("application/json", "stack_trace")
  stackTrace?: StackTrace;

  @doc("Exception cause/inner exception")
  cause?: EnrichedException;

  @doc("Additional exception data")
  data?: Attribute[];

  @doc("Exception fingerprint (for grouping)")
  fingerprint?: string;

  @doc("First occurrence timestamp")
  @encodedName("application/json", "first_seen")
  firstSeen?: utcDateTime;

  @doc("Last occurrence timestamp")
  @encodedName("application/json", "last_seen")
  lastSeen?: utcDateTime;

  @doc("Occurrence count")
  @encodedName("application/json", "occurrence_count")
  occurrenceCount?: Count;

  @doc("Affected users count")
  @encodedName("application/json", "affected_users")
  affectedUsers?: Count;

  @doc("Status")
  status?: ExceptionStatus;
}

@doc("Exception status")
enum ExceptionStatus {
  @doc("New/unreviewed exception")
  new: "new",

  @doc("Exception is being investigated")
  investigating: "investigating",

  @doc("Exception is being fixed")
  inProgress: "in_progress",

  @doc("Exception is resolved")
  resolved: "resolved",

  @doc("Exception is ignored/muted")
  ignored: "ignored",

  @doc("Exception regressed (reoccurred after resolution)")
  regressed: "regressed",
}

// =============================================================================
// Exception Metrics
// =============================================================================

@doc("Exception count metric")
model ExceptionCountMetric {
  @doc("Metric name")
  name: "exceptions";

  @doc("Metric unit")
  unit: "{exception}";

  @doc("Exception type")
  @encodedName("application/json", "exception.type")
  exceptionType: string;

  @doc("Whether exception escaped")
  @encodedName("application/json", "exception.escaped")
  escaped?: boolean;
}

// =============================================================================
// Exception Statistics
// =============================================================================

@doc("Exception statistics")
model ExceptionStats {
  @doc("Total exception count")
  @encodedName("application/json", "total_count")
  totalCount: Count;

  @doc("Unique exception types")
  @encodedName("application/json", "unique_types")
  uniqueTypes: int32;

  @doc("Exceptions by type")
  @encodedName("application/json", "by_type")
  byType: ExceptionTypeStats[];

  @doc("Most affected services")
  @encodedName("application/json", "by_service")
  byService?: ExceptionServiceStats[];

  @doc("Exception trend (up/down/stable)")
  trend?: ExceptionTrend;
}

@doc("Exception stats by type")
model ExceptionTypeStats {
  @doc("Exception type")
  @encodedName("application/json", "exception_type")
  exceptionType: string;

  @doc("Count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;

  @doc("Status")
  status: ExceptionStatus;
}

@doc("Exception stats by service")
model ExceptionServiceStats {
  @doc("Service name")
  @encodedName("application/json", "service_name")
  serviceName: string;

  @doc("Exception count")
  count: Count;

  @doc("Exception rate (per minute)")
  @encodedName("application/json", "rate_per_minute")
  ratePerMinute: float64;
}

@doc("Exception trend")
enum ExceptionTrend {
  @doc("Exceptions increasing")
  up: "up",

  @doc("Exceptions decreasing")
  down: "down",

  @doc("Exceptions stable")
  stable: "stable",
}
