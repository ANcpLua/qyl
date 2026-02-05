// =============================================================================
// QYL v2.0 - API Routes
// =============================================================================
// RESTful API routes with proper pagination, streaming, and error handling.
// =============================================================================

import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi";
import "@typespec/versioning";

import "../common/types.tsp";
import "../common/errors.tsp";
import "../common/pagination.tsp";
import "../otel/span.tsp";
import "../otel/logs.tsp";
import "../otel/metrics.tsp";
import "../domains/observe/session.tsp";
import "../domains/observe/log.tsp";
import "../domains/observe/error.tsp";
import "../domains/ops/deployment.tsp";
import "../domains/identity/user.tsp";
import "../otel/storage.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;
using TypeSpec.Versioning;

using Qyl.Common;
using Qyl.Common.Errors;
using Qyl.Common.Pagination;
using Qyl.OTel.Traces;
using Qyl.OTel.Logs;
using Qyl.OTel.Enums;
using Qyl.Storage;
using Qyl.Domains.Observe.Session;
using Qyl.Domains.Observe.Log;
using Qyl.Domains.Observe.Error;
using Qyl.Domains.Ops.Deployment;
using Qyl.Domains.Identity;

// =============================================================================
// API Versioning
// =============================================================================

@versioned(ApiVersions)
@service(#{
  title: "QYL Observability API",
})
@server("https://api.qyl.dev", "QYL Production API")
@server("https://api.staging.qyl.dev", "QYL Staging API")
namespace Qyl.Api;

enum ApiVersions {
  @doc("Initial API version")
  v1: "2025-12-01",

  @doc("GenAI observability additions")
  v2: "2026-01-15",

  @doc("Full OTel v1.39 compliance")
  v3: "2026-01-26",
}

// =============================================================================
// Common Response Models
// =============================================================================

@doc("Health check response")
model HealthResponse {
  @doc("Service status")
  status: HealthStatus;

  @doc("Service version")
  version: SemVer;

  @doc("Uptime in seconds")
  @encodedName("application/json", "uptime_seconds")
  uptimeSeconds: int64;

  @doc("Component health")
  components?: Record<HealthStatus>;
}

enum HealthStatus {
  healthy: "healthy",
  degraded: "degraded",
  unhealthy: "unhealthy",
}

// =============================================================================
// Traces API
// =============================================================================

@route("/api/v1/traces")
@tag("Traces")
interface TracesApi {
  @doc("List traces with pagination")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Minimum duration in ms") minDurationMs?: int64,
    @query @doc("Maximum duration in ms") maxDurationMs?: int64,
    @query @doc("Status filter") status?: SpanStatusCode,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor for pagination") cursor?: string,
  ): CursorPage<Trace> | NotFoundError | ValidationError | InternalServerError;

  @doc("Get a specific trace by ID")
  @get
  @route("/{traceId}")
  get(@path traceId: TraceId): Trace | NotFoundError | InternalServerError;

  @doc("Get spans for a trace")
  @get
  @route("/{traceId}/spans")
  getSpans(
    @path traceId: TraceId,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<SpanRecord> | NotFoundError | InternalServerError;

  @doc("Search traces")
  @post
  @route("/search")
  search(@body query: TraceQuery): CursorPage<Trace> | ValidationError | InternalServerError;
}

@doc("Trace search query")
model TraceQuery {
  @doc("Free text search")
  query?: string;

  @doc("Service name filter")
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Operation name filter")
  @encodedName("application/json", "operation_name")
  operationName?: string;

  @doc("Minimum duration in milliseconds")
  @encodedName("application/json", "min_duration_ms")
  minDurationMs?: int64;

  @doc("Maximum duration in milliseconds")
  @encodedName("application/json", "max_duration_ms")
  maxDurationMs?: int64;

  @doc("Status filter")
  status?: SpanStatusCode;

  @doc("Time range start")
  @encodedName("application/json", "start_time")
  startTime?: utcDateTime;

  @doc("Time range end")
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @doc("Tag filters")
  tags?: Record<string>;

  @doc("Page size")
  @minValue(1)
  @maxValue(1000)
  limit?: int32 = 100;

  @doc("Cursor")
  cursor?: string;
}

// =============================================================================
// Logs API
// =============================================================================

@route("/api/v1/logs")
@tag("Logs")
interface LogsApi {
  @doc("Query logs with filtering and pagination")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Minimum severity") severityMin?: SeverityNumber,
    @query @doc("Maximum severity") severityMax?: SeverityNumber,
    @query @doc("Trace ID filter") traceId?: TraceId,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Free text search") query?: string,
    @query @doc("Page size") @minValue(1) @maxValue(10000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
    @query @doc("Order") orderBy?: LogOrderBy,
  ): CursorPage<LogRecord> | ValidationError | InternalServerError;

  @doc("Search logs with complex query")
  @post
  @route("/search")
  search(@body query: LogQuery): CursorPage<LogRecord> | ValidationError | InternalServerError;

  @doc("Aggregate logs")
  @post
  @route("/aggregate")
  aggregate(@body request: LogAggregationRequest): LogAggregationResponse | ValidationError | InternalServerError;

  @doc("Get log patterns")
  @get
  @route("/patterns")
  getPatterns(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Minimum occurrences") minCount?: int32,
  ): LogPattern[] | InternalServerError;

  @doc("Get log statistics")
  @get
  @route("/stats")
  getStats(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
  ): Qyl.OTel.Logs.LogStats | InternalServerError;
}

@doc("Log aggregation request")
model LogAggregationRequest {
  @doc("Query filters")
  query?: LogQuery;

  @doc("Aggregation specification")
  aggregation: LogAggregation;
}

@doc("Log aggregation response")
model LogAggregationResponse {
  @doc("Aggregation results")
  results: LogAggregationBucket[];

  @doc("Total matching logs")
  @encodedName("application/json", "total_count")
  totalCount: Count;
}

@doc("Log aggregation bucket")
model LogAggregationBucket {
  @doc("Bucket key (group by value)")
  key: string;

  @doc("Aggregated value")
  value: float64;

  @doc("Document count")
  count: Count;

  @doc("Timestamp (for time series)")
  timestamp?: utcDateTime;
}

// =============================================================================
// Metrics API
// =============================================================================

@route("/api/v1/metrics")
@tag("Metrics")
interface MetricsApi {
  @doc("List available metrics")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Metric name pattern") namePattern?: string,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<MetricMetadata> | InternalServerError;

  @doc("Query metric data points")
  @post
  @route("/query")
  query(@body request: MetricQueryRequest): MetricQueryResponse | ValidationError | InternalServerError;

  @doc("Get metric metadata")
  @get
  @route("/{metricName}")
  getMetadata(@path metricName: string): MetricMetadata | NotFoundError | InternalServerError;
}

@doc("Metric metadata")
model MetricMetadata {
  @doc("Metric name")
  name: string;

  @doc("Metric description")
  description?: string;

  @doc("Metric unit")
  unit?: string;

  @doc("Metric type")
  type: MetricType;

  @doc("Available label keys")
  @encodedName("application/json", "label_keys")
  labelKeys: string[];

  @doc("Services reporting this metric")
  services: string[];
}

@doc("Metric query request")
model MetricQueryRequest {
  @doc("Metric name")
  @encodedName("application/json", "metric_name")
  metricName: string;

  @doc("Label filters")
  filters?: Record<string>;

  @doc("Start time")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("End time")
  @encodedName("application/json", "end_time")
  endTime: utcDateTime;

  @doc("Step interval")
  step?: Qyl.Common.Pagination.TimeBucket;

  @doc("Aggregation function")
  aggregation?: Qyl.OTel.Metrics.AggregationFunction;

  @doc("Group by labels")
  @encodedName("application/json", "group_by")
  groupBy?: string[];
}

@doc("Metric query response")
model MetricQueryResponse {
  @doc("Metric name")
  @encodedName("application/json", "metric_name")
  metricName: string;

  @doc("Time series data")
  series: MetricTimeSeries[];
}

@doc("Metric time series")
model MetricTimeSeries {
  @doc("Labels")
  labels: Record<string>;

  @doc("Data points")
  points: MetricDataPoint[];
}

@doc("Metric data point")
model MetricDataPoint {
  @doc("Timestamp")
  timestamp: utcDateTime;

  @doc("Value")
  value: float64;
}

// =============================================================================
// Sessions API
// =============================================================================

@route("/api/v1/sessions")
@tag("Sessions")
interface SessionsApi {
  @doc("List sessions")
  @get
  list(
    @query @doc("User ID filter") userId?: UserId,
    @query @doc("Is active filter") isActive?: boolean,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<SessionEntity> | ValidationError | InternalServerError;

  @doc("Get session by ID")
  @get
  @route("/{sessionId}")
  get(@path sessionId: string): SessionEntity | NotFoundError | InternalServerError;

  @doc("Get session traces")
  @get
  @route("/{sessionId}/traces")
  getTraces(
    @path sessionId: string,
    @query @doc("Page size") limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<Trace> | NotFoundError | InternalServerError;

  @doc("Get session statistics")
  @get
  @route("/stats")
  getStats(
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
  ): SessionStats | InternalServerError;
}

// =============================================================================
// Errors API
// =============================================================================

@route("/api/v1/errors")
@tag("Errors")
interface ErrorsApi {
  @doc("List error groups")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Status filter") status?: ErrorStatus,
    @query @doc("Category filter") category?: ErrorCategory,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<ErrorEntity> | ValidationError | InternalServerError;

  @doc("Get error by ID")
  @get
  @route("/{errorId}")
  get(@path errorId: string): ErrorEntity | NotFoundError | InternalServerError;

  @doc("Update error status")
  @patch(#{ implicitOptionality: true })
  @route("/{errorId}")
  update(
    @path errorId: string,
    @body update: ErrorUpdate,
  ): ErrorEntity | NotFoundError | ValidationError | InternalServerError;

  @doc("Get error statistics")
  @get
  @route("/stats")
  getStats(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
  ): ErrorStats | InternalServerError;

  @doc("Get error correlations")
  @get
  @route("/{errorId}/correlations")
  getCorrelations(@path errorId: string): ErrorCorrelation | NotFoundError | InternalServerError;
}

@doc("Error update request")
model ErrorUpdate {
  @doc("New status")
  status?: ErrorStatus;

  @doc("Assignee")
  @encodedName("application/json", "assigned_to")
  assignedTo?: string;

  @doc("Issue URL")
  @encodedName("application/json", "issue_url")
  issueUrl?: UrlString;
}

// =============================================================================
// Deployments API
// =============================================================================

@route("/api/v1/deployments")
@tag("Deployments")
interface DeploymentsApi {
  @doc("List deployments")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Environment filter") environment?: DeploymentEnvironment,
    @query @doc("Status filter") status?: DeploymentStatus,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<DeploymentEntity> | ValidationError | InternalServerError;

  @doc("Get deployment by ID")
  @get
  @route("/{deploymentId}")
  get(@path deploymentId: string): DeploymentEntity | NotFoundError | InternalServerError;

  @doc("Record new deployment")
  @post
  create(@body deployment: DeploymentCreate): DeploymentEntity | ValidationError | InternalServerError;

  @doc("Update deployment status")
  @patch(#{ implicitOptionality: true })
  @route("/{deploymentId}")
  update(
    @path deploymentId: string,
    @body update: DeploymentUpdate,
  ): DeploymentEntity | NotFoundError | ValidationError | InternalServerError;

  @doc("Get DORA metrics")
  @get
  @route("/metrics/dora")
  getDoraMetrics(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Environment filter") environment?: DeploymentEnvironment,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
  ): DoraMetrics | InternalServerError;
}

@doc("Deployment creation request")
model DeploymentCreate {
  @doc("Service name")
  @encodedName("application/json", "service_name")
  serviceName: string;

  @doc("Service version")
  @encodedName("application/json", "service_version")
  serviceVersion: SemVer;

  @doc("Environment")
  environment: DeploymentEnvironment;

  @doc("Strategy")
  strategy: DeploymentStrategy;

  @doc("Deployed by")
  @encodedName("application/json", "deployed_by")
  deployedBy?: string;

  @doc("Git commit SHA")
  @encodedName("application/json", "git_commit")
  gitCommit?: string;

  @doc("Git branch")
  @encodedName("application/json", "git_branch")
  gitBranch?: string;
}

@doc("Deployment update request")
model DeploymentUpdate {
  @doc("New status")
  status?: DeploymentStatus;

  @doc("Healthy replicas")
  @encodedName("application/json", "healthy_replicas")
  healthyReplicas?: int32;

  @doc("Error message")
  @encodedName("application/json", "error_message")
  errorMessage?: string;
}

@doc("DORA metrics response")
model DoraMetrics {
  @doc("Deployment frequency (per day)")
  @encodedName("application/json", "deployment_frequency")
  deploymentFrequency: float64;

  @doc("Lead time for changes (hours)")
  @encodedName("application/json", "lead_time_hours")
  leadTimeHours: float64;

  @doc("Change failure rate")
  @encodedName("application/json", "change_failure_rate")
  changeFailureRate: Ratio;

  @doc("Mean time to recovery (hours)")
  @encodedName("application/json", "mttr_hours")
  mttrHours: float64;

  @doc("Performance level")
  @encodedName("application/json", "performance_level")
  performanceLevel: DoraPerformanceLevel;
}

@doc("DORA performance levels")
enum DoraPerformanceLevel {
  @doc("Elite performer")
  elite: "elite",

  @doc("High performer")
  high: "high",

  @doc("Medium performer")
  medium: "medium",

  @doc("Low performer")
  low: "low",
}

// =============================================================================
// Services API
// =============================================================================

@route("/api/v1/services")
@tag("Services")
interface ServicesApi {
  @doc("List discovered services")
  @get
  list(
    @query @doc("Namespace filter") namespaceName?: string,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<ServiceInfo> | InternalServerError;

  @doc("Get service details")
  @get
  @route("/{serviceName}")
  get(@path serviceName: string): ServiceDetails | NotFoundError | InternalServerError;

  @doc("Get service dependencies")
  @get
  @route("/{serviceName}/dependencies")
  getDependencies(@path serviceName: string): ServiceDependency[] | NotFoundError | InternalServerError;

  @doc("Get service operations")
  @get
  @route("/{serviceName}/operations")
  getOperations(
    @path serviceName: string,
    @query @doc("Page size") limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<OperationInfo> | NotFoundError | InternalServerError;
}

@doc("Service information")
model ServiceInfo {
  @doc("Service name")
  name: string;

  @doc("Service namespace")
  @encodedName("application/json", "namespace_name")
  namespaceName?: string;

  @doc("Service version")
  version?: SemVer;

  @doc("Instance count")
  @encodedName("application/json", "instance_count")
  instanceCount: int32;

  @doc("Last seen")
  @encodedName("application/json", "last_seen")
  lastSeen: utcDateTime;
}

@doc("Service details")
model ServiceDetails {
  ...ServiceInfo;

  @doc("Resource attributes")
  @encodedName("application/json", "resource_attributes")
  resourceAttributes: Attribute[];

  @doc("Instrumentation libraries")
  @encodedName("application/json", "instrumentation_libraries")
  instrumentationLibraries: InstrumentationScope[];

  @doc("Request rate (per second)")
  @encodedName("application/json", "request_rate")
  requestRate: float64;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Average latency in milliseconds")
  @encodedName("application/json", "avg_latency_ms")
  avgLatencyMs: float64;

  @doc("P99 latency in milliseconds")
  @encodedName("application/json", "p99_latency_ms")
  p99LatencyMs: float64;
}

@doc("Operation information")
model OperationInfo {
  @doc("Operation name")
  name: string;

  @doc("Span kind")
  @encodedName("application/json", "span_kind")
  spanKind: SpanKind;

  @doc("Request count")
  @encodedName("application/json", "request_count")
  requestCount: Count;

  @doc("Error count")
  @encodedName("application/json", "error_count")
  errorCount: Count;

  @doc("Average duration in milliseconds")
  @encodedName("application/json", "avg_duration_ms")
  avgDurationMs: float64;

  @doc("P99 duration in milliseconds")
  @encodedName("application/json", "p99_duration_ms")
  p99DurationMs: float64;
}

// =============================================================================
// Health API
// =============================================================================

@route("/health")
@tag("Health")
interface HealthApi {
  @doc("Health check endpoint")
  @get
  @operationId("health_check")
  check(): HealthResponse;

  @doc("Liveness probe")
  @get
  @route("/live")
  @operationId("health_liveness")
  live(): {
    @statusCode statusCode: 200;
  } | {
    @statusCode statusCode: 503;
  };

  @doc("Readiness probe")
  @get
  @route("/ready")
  @operationId("health_readiness")
  ready(): {
    @statusCode statusCode: 200;
  } | {
    @statusCode statusCode: 503;
  };
}
