/**
 * A sequence of textual characters.
 */
export type String = string;
/**
 * Stream event types
 */
export enum StreamEventType {
  /**
   * Trace events
   */
  Traces = "traces",
  /**
   * Span events
   */
  Spans = "spans",
  /**
   * Log events
   */
  Logs = "logs",
  /**
   * Metric events
   */
  Metrics = "metrics",
  /**
   * Deployment events
   */
  Deployments = "deployments",
  /**
   * All events
   */
  All = "all"
}
/**
 * A 64 bit floating point number. (`±5.0 × 10^−324` to `±1.7 × 10^308`)
 */
export type Float64 = number;
/**
 * A number with decimal value
 */
export type Float = number;
/**
 * A numeric type
 */
export type Numeric = number;
/**
 * A 64-bit integer. (`-9,223,372,036,854,775,808` to `9,223,372,036,854,775,807`)
 */
export type Int64 = bigint;
/**
 * A whole number. This represent any `integer` value possible.
 * It is commonly represented as `BigInteger` in some languages.
 */
export type Integer = number;
/**
 * Unique trace identifier (32 lowercase hex characters)
 */
export type TraceId = string;
/**
 * A 32-bit integer. (`-2,147,483,648` to `2,147,483,647`)
 */
export type Int32 = number;
/**
 * Span status code
 */
export enum SpanStatusCode {
  /**
   * Status not set
   */
  Unset = 0,
  /**
   * Operation completed successfully
   */
  Ok = 1,
  /**
   * Operation failed with an error
   */
  Error = 2
}
/**
 * An instant in coordinated universal time (UTC)"
 */
export type UtcDateTime = Date;
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage {
  /**
   * List of items in this page
   */
  items: Array<Trace>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Complete trace containing all related spans
 */
export interface Trace {
  /**
   * Trace identifier
   */
  traceId: string;
  /**
   * All spans in this trace
   */
  spans: Array<Span>;
  /**
   * Root span of the trace
   */
  rootSpan?: Span;
  /**
   * Total span count
   */
  spanCount: number;
  /**
   * Trace duration in nanoseconds
   */
  durationNs: bigint;
  /**
   * Trace start time
   */
  startTime: Date;
  /**
   * Trace end time
   */
  endTime: Date;
  /**
   * Services involved in this trace
   */
  services: Array<string>;
  /**
   * Whether trace contains errors
   */
  hasError: boolean;
}
/**
 * OpenTelemetry Span representing a single operation in a distributed trace
 */
export interface Span {
  /**
   * Unique span identifier (16 hex chars)
   */
  spanId: string;
  /**
   * Trace identifier (32 hex chars)
   */
  traceId: string;
  /**
   * Parent span identifier (null for root spans)
   */
  parentSpanId?: string;
  /**
   * W3C trace state
   */
  traceState?: string;
  /**
   * Human-readable span name
   */
  name: string;
  /**
   * Span kind
   */
  kind: SpanKind;
  /**
   * Start timestamp in nanoseconds since epoch
   */
  startTimeUnixNano: bigint;
  /**
   * End timestamp in nanoseconds since epoch
   */
  endTimeUnixNano: bigint;
  /**
   * Span attributes
   */
  attributes?: Array<Attribute>;
  /**
   * Dropped attributes count
   */
  droppedAttributesCount?: bigint;
  /**
   * Span events (logs attached to span)
   */
  events?: Array<SpanEvent>;
  /**
   * Dropped events count
   */
  droppedEventsCount?: bigint;
  /**
   * Links to other spans
   */
  links?: Array<SpanLink>;
  /**
   * Dropped links count
   */
  droppedLinksCount?: bigint;
  /**
   * Span status
   */
  status: SpanStatus;
  /**
   * Span flags
   */
  flags?: number;
  /**
   * Resource describing the entity that produced this span
   */
  resource: Resource;
  /**
   * Instrumentation scope
   */
  instrumentationScope?: InstrumentationScope;
}
/**
 * Unique span identifier (16 lowercase hex characters)
 */
export type SpanId = string;
/**
 * W3C Trace Context tracestate header (vendor-specific key-value pairs)
 */
export type TraceState = string;
/**
 * Span kind describing the relationship between spans
 */
export enum SpanKind {
  /**
   * Default. Internal operation within the application
   */
  Unspecified = 0,
  /**
   * Internal operation within the application
   */
  Internal = 1,
  /**
   * Server-side operation handling a request
   */
  Server = 2,
  /**
   * Client-side operation making a request
   */
  Client = 3,
  /**
   * Producer creating a message for async processing
   */
  Producer = 4,
  /**
   * Consumer processing a message
   */
  Consumer = 5
}
/**
 * Key-value attribute pair following OTel conventions
 */
export interface Attribute {
  /**
   * Attribute key (dot-separated namespace)
   */
  key: string;
  /**
   * Attribute value
   */
  value: AttributeValue;
}
/**
 * Primitive attribute value types supported by OpenTelemetry
 */
export type AttributeValue = string | boolean | bigint | number | Array<string> | Array<boolean> | Array<bigint> | Array<number> | Uint8Array;
/**
 * Boolean with `true` and `false` values.
 */
export type Boolean = boolean;
/**
 * Represent a byte array
 */
export type Bytes = Uint8Array;
/**
 * Generic non-negative counter
 */
export type Count = bigint;
/**
 * Event occurring during a span's lifetime
 */
export interface SpanEvent {
  /**
   * Event name
   */
  name: string;
  /**
   * Event timestamp in nanoseconds since epoch
   */
  timeUnixNano: bigint;
  /**
   * Event attributes
   */
  attributes?: Array<Attribute>;
  /**
   * Dropped attributes count
   */
  droppedAttributesCount?: bigint;
}
/**
 * Link to another span (e.g., batch processing)
 */
export interface SpanLink {
  /**
   * Linked trace ID
   */
  traceId: string;
  /**
   * Linked span ID
   */
  spanId: string;
  /**
   * Trace state of the linked span
   */
  traceState?: string;
  /**
   * Link attributes
   */
  attributes?: Array<Attribute>;
  /**
   * Dropped attributes count
   */
  droppedAttributesCount?: bigint;
  /**
   * Link flags
   */
  flags?: number;
}
/**
 * Span status
 */
export interface SpanStatus {
  /**
   * Status code
   */
  code: SpanStatusCode;
  /**
   * Status message (only for ERROR status)
   */
  message?: string;
}
/**
 * Resource describes the entity producing telemetry
 */
export interface Resource {
  /**
   * Service name (required)
   */
  serviceName: string;
  /**
   * Service namespace for grouping
   */
  serviceNamespace?: string;
  /**
   * Service instance ID (unique per instance)
   */
  serviceInstanceId?: string;
  /**
   * Service version
   */
  serviceVersion?: string;
  /**
   * Telemetry SDK name
   */
  telemetrySdkName?: string;
  /**
   * Telemetry SDK language
   */
  telemetrySdkLanguage?: TelemetrySdkLanguage;
  /**
   * Telemetry SDK version
   */
  telemetrySdkVersion?: string;
  /**
   * Auto-instrumentation agent name
   */
  telemetryAutoVersion?: string;
  /**
   * Deployment environment (e.g., production, staging)
   */
  deploymentEnvironment?: string;
  /**
   * Cloud provider
   */
  cloudProvider?: CloudProvider;
  /**
   * Cloud region
   */
  cloudRegion?: string;
  /**
   * Cloud availability zone
   */
  cloudAvailabilityZone?: string;
  /**
   * Cloud account ID
   */
  cloudAccountId?: string;
  /**
   * Cloud platform (e.g., aws_ecs, gcp_cloud_run)
   */
  cloudPlatform?: string;
  /**
   * Host name
   */
  hostName?: string;
  /**
   * Host ID
   */
  hostId?: string;
  /**
   * Host type (e.g., n1-standard-1)
   */
  hostType?: string;
  /**
   * Host architecture (e.g., amd64, arm64)
   */
  hostArch?: HostArch;
  /**
   * Operating system type
   */
  osType?: OsType;
  /**
   * Operating system description
   */
  osDescription?: string;
  /**
   * Operating system version
   */
  osVersion?: string;
  /**
   * Process ID
   */
  processPid?: bigint;
  /**
   * Process executable name
   */
  processExecutableName?: string;
  /**
   * Process command line
   */
  processCommandLine?: string;
  /**
   * Process runtime name
   */
  processRuntimeName?: string;
  /**
   * Process runtime version
   */
  processRuntimeVersion?: string;
  /**
   * Container ID
   */
  containerId?: string;
  /**
   * Container name
   */
  containerName?: string;
  /**
   * Container image name
   */
  containerImageName?: string;
  /**
   * Container image tag
   */
  containerImageTag?: string;
  /**
   * Kubernetes cluster name
   */
  k8sClusterName?: string;
  /**
   * Kubernetes namespace
   */
  k8sNamespaceName?: string;
  /**
   * Kubernetes pod name
   */
  k8sPodName?: string;
  /**
   * Kubernetes pod UID
   */
  k8sPodUid?: string;
  /**
   * Kubernetes deployment name
   */
  k8sDeploymentName?: string;
  /**
   * Additional resource attributes
   */
  attributes?: Array<Attribute>;
  /**
   * Dropped attributes count
   */
  droppedAttributesCount?: bigint;
}
/**
 * Semantic version string (e.g., 1.2.3)
 */
export type SemVer = string;
/**
 * Telemetry SDK language
 */
export enum TelemetrySdkLanguage {
  /**
   * C++
   */
  Cpp = "cpp",
  /**
   * .NET (C#, F#, VB.NET)
   */
  Dotnet = "dotnet",
  /**
   * Erlang
   */
  Erlang = "erlang",
  /**
   * Go
   */
  Go = "go",
  /**
   * Java
   */
  Java = "java",
  /**
   * Node.js
   */
  Nodejs = "nodejs",
  /**
   * PHP
   */
  Php = "php",
  /**
   * Python
   */
  Python = "python",
  /**
   * Ruby
   */
  Ruby = "ruby",
  /**
   * Rust
   */
  Rust = "rust",
  /**
   * Swift
   */
  Swift = "swift",
  /**
   * Web browser JavaScript
   */
  Webjs = "webjs"
}
/**
 * Cloud provider types
 */
export enum CloudProvider {
  /**
   * Alibaba Cloud
   */
  AlibabaCloud = "alibaba_cloud",
  /**
   * Amazon Web Services
   */
  Aws = "aws",
  /**
   * Microsoft Azure
   */
  Azure = "azure",
  /**
   * Google Cloud Platform
   */
  Gcp = "gcp",
  /**
   * Heroku
   */
  Heroku = "heroku",
  /**
   * IBM Cloud
   */
  IbmCloud = "ibm_cloud",
  /**
   * Tencent Cloud
   */
  TencentCloud = "tencent_cloud"
}
/**
 * Host architecture types
 */
export enum HostArch {
  /**
   * AMD64 / x86_64
   */
  Amd64 = "amd64",
  /**
   * ARM32
   */
  Arm32 = "arm32",
  /**
   * ARM64 / aarch64
   */
  Arm64 = "arm64",
  /**
   * Itanium
   */
  Ia64 = "ia64",
  /**
   * PowerPC 32-bit
   */
  Ppc32 = "ppc32",
  /**
   * PowerPC 64-bit
   */
  Ppc64 = "ppc64",
  /**
   * IBM z/Architecture
   */
  S390x = "s390x",
  /**
   * x86 32-bit
   */
  X86 = "x86"
}
/**
 * Operating system types
 */
export enum OsType {
  /**
   * Microsoft Windows
   */
  Windows = "windows",
  /**
   * Linux
   */
  Linux = "linux",
  /**
   * Apple macOS
   */
  Darwin = "darwin",
  /**
   * FreeBSD
   */
  Freebsd = "freebsd",
  /**
   * NetBSD
   */
  Netbsd = "netbsd",
  /**
   * OpenBSD
   */
  Openbsd = "openbsd",
  /**
   * DragonFly BSD
   */
  Dragonflybsd = "dragonflybsd",
  /**
   * HP-UX
   */
  Hpux = "hpux",
  /**
   * AIX
   */
  Aix = "aix",
  /**
   * Oracle Solaris
   */
  Solaris = "solaris",
  /**
   * IBM z/OS
   */
  ZOs = "z_os"
}
/**
 * Instrumentation scope identifying the library/component emitting telemetry
 */
export interface InstrumentationScope {
  /**
   * Name of the instrumentation scope (library name)
   */
  scopeName: string;
  /**
   * Version of the instrumentation scope
   */
  scopeVersion?: string;
  /**
   * Additional attributes for the scope
   */
  scopeAttributes?: Array<Attribute>;
  /**
   * Dropped attributes count
   */
  droppedAttributesCount?: bigint;
}
/**
 * Duration in nanoseconds
 */
export type DurationNs = bigint;
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_2 {
  /**
   * List of items in this page
   */
  items: Array<SpanRecord>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * OpenTelemetry span record for storage and query
 */
export interface SpanRecord {
  /**
   * Unique span identifier
   */
  spanId: string;
  /**
   * Trace identifier
   */
  traceId: string;
  /**
   * Parent span identifier (null for root spans)
   */
  parentSpanId?: string;
  /**
   * Session identifier for grouping related traces
   */
  sessionId?: string;
  /**
   * Human-readable span name
   */
  name: string;
  /**
   * Span kind
   */
  kind: SpanKind;
  /**
   * Start timestamp in nanoseconds since epoch
   */
  startTimeUnixNano: bigint;
  /**
   * End timestamp in nanoseconds since epoch
   */
  endTimeUnixNano: bigint;
  /**
   * Duration in nanoseconds
   */
  durationNs: bigint;
  /**
   * Span status code
   */
  statusCode: SpanStatusCode;
  /**
   * Status message (only for ERROR status)
   */
  statusMessage?: string;
  /**
   * Service name from resource attributes
   */
  serviceName?: string;
  /**
   * GenAI provider name (e.g., openai, anthropic) - OTel 1.40: gen_ai.provider.name
   */
  genAiProviderName?: string;
  /**
   * Requested model name
   */
  genAiRequestModel?: string;
  /**
   * Actual response model name
   */
  genAiResponseModel?: string;
  /**
   * Input/prompt tokens
   */
  genAiInputTokens?: bigint;
  /**
   * Output/completion tokens
   */
  genAiOutputTokens?: bigint;
  /**
   * Request temperature
   */
  genAiTemperature?: number;
  /**
   * Response finish reason
   */
  genAiStopReason?: string;
  /**
   * Tool name for tool calls
   */
  genAiToolName?: string;
  /**
   * Tool call ID
   */
  genAiToolCallId?: string;
  /**
   * Estimated cost in USD
   */
  genAiCostUsd?: number;
  /**
   * All span attributes as JSON
   */
  attributesJson?: string;
  /**
   * Resource attributes as JSON
   */
  resourceJson?: string;
  /**
   * W3C Baggage key-value pairs as JSON for cross-cutting concern propagation
   */
  baggageJson?: string;
  /**
   * OTel semantic convention schema URL (e.g., https://opentelemetry.io/schemas/1.40.0)
   */
  schemaUrl?: string;
  /**
   * Row creation timestamp
   */
  createdAt?: Date;
}
/**
 * Unique session identifier
 */
export type SessionId = string;
/**
 * Token count (for LLM operations)
 */
export type TokenCount = bigint;
/**
 * Temperature setting for LLM requests (0.0-2.0)
 */
export type Temperature = number;
/**
 * Cost in USD (floating point)
 */
export type CostUsd = number;
/**
 * Trace search query
 */
export interface TraceQuery {
  /**
   * Free text search
   */
  query?: string;
  /**
   * Service name filter
   */
  serviceName?: string;
  /**
   * Operation name filter
   */
  operationName?: string;
  /**
   * Minimum duration in milliseconds
   */
  minDurationMs?: bigint;
  /**
   * Maximum duration in milliseconds
   */
  maxDurationMs?: bigint;
  /**
   * Status filter
   */
  status?: SpanStatusCode;
  /**
   * Time range start
   */
  startTime?: Date;
  /**
   * Time range end
   */
  endTime?: Date;
  /**
   * Tag filters
   */
  tags?: Record<string, string>;
  /**
   * Page size
   */
  limit?: number;
  /**
   * Cursor
   */
  cursor?: string;
}
/**
 * Log severity number following OTel specification (1-24)
 */
export enum SeverityNumber {
  /**
   * Unspecified severity
   */
  Unspecified = 0,
  /**
   * TRACE level
   */
  Trace = 1,
  /**
   * TRACE2 level
   */
  Trace2 = 2,
  /**
   * TRACE3 level
   */
  Trace3 = 3,
  /**
   * TRACE4 level
   */
  Trace4 = 4,
  /**
   * DEBUG level
   */
  Debug = 5,
  /**
   * DEBUG2 level
   */
  Debug2 = 6,
  /**
   * DEBUG3 level
   */
  Debug3 = 7,
  /**
   * DEBUG4 level
   */
  Debug4 = 8,
  /**
   * INFO level
   */
  Info = 9,
  /**
   * INFO2 level
   */
  Info2 = 10,
  /**
   * INFO3 level
   */
  Info3 = 11,
  /**
   * INFO4 level
   */
  Info4 = 12,
  /**
   * WARN level
   */
  Warn = 13,
  /**
   * WARN2 level
   */
  Warn2 = 14,
  /**
   * WARN3 level
   */
  Warn3 = 15,
  /**
   * WARN4 level
   */
  Warn4 = 16,
  /**
   * ERROR level
   */
  Error = 17,
  /**
   * ERROR2 level
   */
  Error2 = 18,
  /**
   * ERROR3 level
   */
  Error3 = 19,
  /**
   * ERROR4 level
   */
  Error4 = 20,
  /**
   * FATAL level
   */
  Fatal = 21,
  /**
   * FATAL2 level
   */
  Fatal2 = 22,
  /**
   * FATAL3 level
   */
  Fatal3 = 23,
  /**
   * FATAL4 level
   */
  Fatal4 = 24
}
/**
 * Log ordering options
 */
export enum LogOrderBy {
  /**
   * Timestamp ascending
   */
  TimestampAsc = "timestamp_asc",
  /**
   * Timestamp descending
   */
  TimestampDesc = "timestamp_desc",
  /**
   * Severity ascending
   */
  SeverityAsc = "severity_asc",
  /**
   * Severity descending
   */
  SeverityDesc = "severity_desc"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_3 {
  /**
   * List of items in this page
   */
  items: Array<LogRecord>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * OpenTelemetry Log Record
 */
export interface LogRecord {
  /**
   * Timestamp when the event occurred (nanoseconds since epoch)
   */
  timeUnixNano: bigint;
  /**
   * Timestamp when the log was observed/collected (nanoseconds since epoch)
   */
  observedTimeUnixNano: bigint;
  /**
   * Severity number (1-24)
   */
  severityNumber: SeverityNumber;
  /**
   * Severity text (DEBUG, INFO, WARN, ERROR, etc.)
   */
  severityText?: SeverityText;
  /**
   * Log body - the main content
   */
  body: LogBody;
  /**
   * Log attributes
   */
  attributes?: Array<Attribute>;
  /**
   * Dropped attributes count
   */
  droppedAttributesCount?: bigint;
  /**
   * Flags (trace flags)
   */
  flags?: number;
  /**
   * Associated trace ID
   */
  traceId?: string;
  /**
   * Associated span ID
   */
  spanId?: string;
  /**
   * Resource describing the entity that produced this log
   */
  resource: Resource;
  /**
   * Instrumentation scope
   */
  instrumentationScope?: InstrumentationScope;
}
/**
 * Log severity text (human-readable)
 */
export enum SeverityText {
  /**
   * Trace level logging
   */
  Trace = "TRACE",
  /**
   * Debug level logging
   */
  Debug = "DEBUG",
  /**
   * Informational logging
   */
  Info = "INFO",
  /**
   * Warning level logging
   */
  Warn = "WARN",
  /**
   * Error level logging
   */
  Error = "ERROR",
  /**
   * Fatal/critical level logging
   */
  Fatal = "FATAL"
}
/**
 * Log body content - can be string, structured, or bytes
 */
export type LogBody = {
  kind: "stringBody"
} & LogBodyString | {
  kind: "kvListBody"
} & LogBodyKvList | {
  kind: "arrayBody"
} & LogBodyArray | {
  kind: "bytesBody"
} & LogBodyBytes;
/**
 * String log body
 */
export interface LogBodyString {
  /**
   * String value
   */
  stringValue: string;
}
/**
 * Structured key-value log body
 */
export interface LogBodyKvList {
  /**
   * Key-value pairs
   */
  kvListValue: Array<Attribute>;
}
/**
 * Array log body
 */
export interface LogBodyArray {
  /**
   * Array of values
   */
  arrayValue: Array<AttributeValue>;
}
/**
 * Binary log body
 */
export interface LogBodyBytes {
  /**
   * Binary value (base64 encoded)
   */
  bytesValue: Uint8Array;
}
/**
 * Log search query
 */
export interface LogQuery {
  /**
   * Free text search
   */
  query?: string;
  /**
   * Severity filter
   */
  severityMin?: SeverityNumber;
  /**
   * Service name filter
   */
  serviceName?: string;
  /**
   * Trace ID filter
   */
  traceId?: string;
  /**
   * Span ID filter
   */
  spanId?: string;
  /**
   * Time range start
   */
  timeStart?: Date;
  /**
   * Time range end
   */
  timeEnd?: Date;
  /**
   * Attribute filters
   */
  attributeFilters?: Array<AttributeFilter>;
  /**
   * Limit
   */
  limit?: number;
  /**
   * Order by
   */
  orderBy?: LogOrderBy;
}
/**
 * Attribute filter
 */
export interface AttributeFilter {
  /**
   * Attribute key
   */
  key: string;
  /**
   * Filter operator
   */
  operator: FilterOperator;
  /**
   * Filter value
   */
  value: string;
}
/**
 * Filter operators
 */
export enum FilterOperator {
  /**
   * Equals
   */
  Eq = "eq",
  /**
   * Not equals
   */
  Neq = "neq",
  /**
   * Contains
   */
  Contains = "contains",
  /**
   * Starts with
   */
  StartsWith = "starts_with",
  /**
   * Ends with
   */
  EndsWith = "ends_with",
  /**
   * Regex match
   */
  Regex = "regex",
  /**
   * Greater than
   */
  Gt = "gt",
  /**
   * Greater than or equal
   */
  Gte = "gte",
  /**
   * Less than
   */
  Lt = "lt",
  /**
   * Less than or equal
   */
  Lte = "lte",
  /**
   * In list
   */
  In = "in",
  /**
   * Not in list
   */
  NotIn = "not_in",
  /**
   * Exists
   */
  Exists = "exists",
  /**
   * Does not exist
   */
  NotExists = "not_exists"
}
/**
 * Log aggregation request
 */
export interface LogAggregationRequest {
  /**
   * Query filters
   */
  query?: LogQuery;
  /**
   * Aggregation specification
   */
  aggregation: LogAggregation;
}
/**
 * Log aggregation request
 */
export interface LogAggregation {
  /**
   * Group by fields
   */
  groupBy: Array<string>;
  /**
   * Aggregation function
   */
  function_: AggregationFunction;
  /**
   * Field to aggregate (for non-count)
   */
  field?: string;
  /**
   * Time bucket (for time series)
   */
  timeBucket?: TimeBucket;
  /**
   * Top N results
   */
  topN?: number;
}
/**
 * Aggregation functions
 */
export enum AggregationFunction {
  /**
   * Count
   */
  Count = "count",
  /**
   * Sum
   */
  Sum = "sum",
  /**
   * Average
   */
  Avg = "avg",
  /**
   * Minimum
   */
  Min = "min",
  /**
   * Maximum
   */
  Max = "max",
  /**
   * Percentile 50
   */
  P50 = "p50",
  /**
   * Percentile 90
   */
  P90 = "p90",
  /**
   * Percentile 95
   */
  P95 = "p95",
  /**
   * Percentile 99
   */
  P99 = "p99",
  /**
   * Count distinct
   */
  CountDistinct = "count_distinct"
}
/**
 * Time bucket sizes
 */
export enum TimeBucket {
  /**
   * 1 second
   */
  S1 = "1s",
  /**
   * 10 seconds
   */
  S10 = "10s",
  /**
   * 30 seconds
   */
  S30 = "30s",
  /**
   * 1 minute
   */
  M1 = "1m",
  /**
   * 5 minutes
   */
  M5 = "5m",
  /**
   * 15 minutes
   */
  M15 = "15m",
  /**
   * 30 minutes
   */
  M30 = "30m",
  /**
   * 1 hour
   */
  H1 = "1h",
  /**
   * 6 hours
   */
  H6 = "6h",
  /**
   * 12 hours
   */
  H12 = "12h",
  /**
   * 1 day
   */
  D1 = "1d",
  /**
   * 1 week
   */
  W1 = "1w"
}
/**
 * Log aggregation response
 */
export interface LogAggregationResponse {
  /**
   * Aggregation results
   */
  results: Array<LogAggregationBucket>;
  /**
   * Total matching logs
   */
  totalCount: bigint;
}
/**
 * Log aggregation bucket
 */
export interface LogAggregationBucket {
  /**
   * Bucket key (group by value)
   */
  key: string;
  /**
   * Aggregated value
   */
  value: number;
  /**
   * Document count
   */
  count: bigint;
  /**
   * Timestamp (for time series)
   */
  timestamp?: Date;
}
/**
 * Detected log pattern
 */
export interface LogPattern {
  /**
   * Pattern ID
   */
  patternId: string;
  /**
   * Pattern template
   */
  template: string;
  /**
   * Sample log message
   */
  sample: string;
  /**
   * Occurrence count
   */
  count: bigint;
  /**
   * First seen
   */
  firstSeen: Date;
  /**
   * Last seen
   */
  lastSeen: Date;
  /**
   * Trend
   */
  trend: LogPatternTrend;
  /**
   * Severity distribution
   */
  severityDistribution?: Array<LogSeverityStats>;
}
/**
 * Log pattern trend
 */
export enum LogPatternTrend {
  /**
   * Increasing
   */
  Increasing = "increasing",
  /**
   * Decreasing
   */
  Decreasing = "decreasing",
  /**
   * Stable
   */
  Stable = "stable",
  /**
   * New pattern
   */
  New = "new",
  /**
   * Anomalous spike
   */
  Spike = "spike"
}
/**
 * Log stats by severity
 */
export interface LogSeverityStats {
  /**
   * Severity number
   */
  severity: SeverityNumber;
  /**
   * Severity text
   */
  severityText: string;
  /**
   * Count
   */
  count: bigint;
  /**
   * Percentage of total
   */
  percentage: number;
}
/**
 * Percentage value (0.0 to 100.0)
 */
export type Percentage = number;
/**
 * Aggregated log statistics
 */
export interface LogStats {
  /**
   * Total log count
   */
  totalCount: bigint;
  /**
   * Log counts by severity
   */
  bySeverity: Array<LogCountBySeverity>;
  /**
   * Log counts by service
   */
  byService: Array<LogCountByDimension>;
  /**
   * Logs per second rate
   */
  logsPerSecond: number;
  /**
   * Error log rate
   */
  errorRate: number;
}
/**
 * Log count by severity level
 */
export interface LogCountBySeverity {
  /**
   * Severity level
   */
  severity: SeverityText;
  /**
   * Log count
   */
  count: bigint;
  /**
   * Percentage of total
   */
  percentage: number;
}
/**
 * Log count by dimension
 */
export interface LogCountByDimension {
  /**
   * Dimension value
   */
  dimension: string;
  /**
   * Log count
   */
  count: bigint;
  /**
   * Error count for this dimension
   */
  errorCount: bigint;
}
/**
 * Ratio value (0.0 to 1.0)
 */
export type Ratio = number;
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_4 {
  /**
   * List of items in this page
   */
  items: Array<MetricMetadata>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Metric metadata
 */
export interface MetricMetadata {
  /**
   * Metric name
   */
  name: string;
  /**
   * Metric description
   */
  description?: string;
  /**
   * Metric unit
   */
  unit?: string;
  /**
   * Metric type
   */
  type: MetricType;
  /**
   * Available label keys
   */
  labelKeys: Array<string>;
  /**
   * Services reporting this metric
   */
  services: Array<string>;
}
/**
 * Metric data type
 */
export enum MetricType {
  /**
   * Gauge - instantaneous value
   */
  Gauge = "gauge",
  /**
   * Sum - monotonic or non-monotonic counter
   */
  Sum = "sum",
  /**
   * Histogram - distribution of values
   */
  Histogram = "histogram",
  /**
   * Exponential histogram - distribution with exponential buckets
   */
  ExponentialHistogram = "exponential_histogram",
  /**
   * Summary - pre-aggregated quantiles
   */
  Summary = "summary"
}
/**
 * Metric query request
 */
export interface MetricQueryRequest {
  /**
   * Metric name
   */
  metricName: string;
  /**
   * Label filters
   */
  filters?: Record<string, string>;
  /**
   * Start time
   */
  startTime: Date;
  /**
   * End time
   */
  endTime: Date;
  /**
   * Step interval
   */
  step?: TimeBucket_2;
  /**
   * Aggregation function
   */
  aggregation?: AggregationFunction_2;
  /**
   * Group by labels
   */
  groupBy?: Array<string>;
}
/**
 * Time bucket size for aggregations
 */
export enum TimeBucket_2 {
  /**
   * 1 minute buckets
   */
  Minute = "1m",
  /**
   * 5 minute buckets
   */
  FiveMinutes = "5m",
  /**
   * 15 minute buckets
   */
  FifteenMinutes = "15m",
  /**
   * 1 hour buckets
   */
  Hour = "1h",
  /**
   * 1 day buckets
   */
  Day = "1d",
  /**
   * 1 week buckets
   */
  Week = "1w",
  /**
   * Auto-select based on time range
   */
  Auto = "auto"
}
/**
 * Aggregation functions for metrics
 */
export enum AggregationFunction_2 {
  /**
   * Sum of values
   */
  Sum = "sum",
  /**
   * Average of values
   */
  Avg = "avg",
  /**
   * Minimum value
   */
  Min = "min",
  /**
   * Maximum value
   */
  Max = "max",
  /**
   * Count of values
   */
  Count = "count",
  /**
   * Latest value
   */
  Last = "last",
  /**
   * Rate of change per second
   */
  Rate = "rate",
  /**
   * Increase over time range
   */
  Increase = "increase"
}
/**
 * Metric query response
 */
export interface MetricQueryResponse {
  /**
   * Metric name
   */
  metricName: string;
  /**
   * Time series data
   */
  series: Array<MetricTimeSeries>;
}
/**
 * Metric time series
 */
export interface MetricTimeSeries {
  /**
   * Labels
   */
  labels: Record<string, string>;
  /**
   * Data points
   */
  points: Array<MetricDataPoint>;
}
/**
 * Metric data point
 */
export interface MetricDataPoint {
  /**
   * Timestamp
   */
  timestamp: Date;
  /**
   * Value
   */
  value: number;
}
/**
 * OpenTelemetry profile record for storage and query
 */
export interface ProfileRecord {
  /**
   * Unique profile identifier
   */
  profileId: string;
  /**
   * Correlated trace ID (from Link table)
   */
  traceId?: string;
  /**
   * Correlated span ID (from Link table)
   */
  spanId?: string;
  /**
   * Session identifier for grouping related profiles
   */
  sessionId?: string;
  /**
   * Profile start timestamp in nanoseconds since epoch
   */
  timeUnixNano: bigint;
  /**
   * Profile duration in nanoseconds
   */
  durationNano: bigint;
  /**
   * Number of samples in this profile
   */
  sampleCount: number;
  /**
   * Sample type (e.g., cpu, alloc_objects, wall)
   */
  sampleType?: string;
  /**
   * Sample unit (e.g., nanoseconds, bytes, count)
   */
  sampleUnit?: string;
  /**
   * Original payload format
   */
  originalPayloadFormat?: string;
  /**
   * Service name from resource attributes
   */
  serviceName?: string;
  /**
   * Profile frame type (dotnet, jvm, cpython, etc.)
   */
  profileFrameType?: string;
  /**
   * Profile attributes as JSON
   */
  attributesJson?: string;
  /**
   * Resource attributes as JSON
   */
  resourceJson?: string;
  /**
   * Full profile structure as JSON blob (denormalized for single-query access)
   */
  profileDataJson?: string;
  /**
   * OTel semantic convention schema URL
   */
  schemaUrl?: string;
  /**
   * Row creation timestamp
   */
  createdAt?: Date;
}
/**
 * User identifier (pseudonymized for privacy)
 */
export type UserId = string;
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_5 {
  /**
   * List of items in this page
   */
  items: Array<SessionEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Complete session entity with aggregated data
 */
export interface SessionEntity {
  /**
   * Session ID
   */
  sessionId: string;
  /**
   * User ID (if authenticated)
   */
  userId?: string;
  /**
   * Session start time
   */
  startTime: Date;
  /**
   * Session end time
   */
  endTime?: Date;
  /**
   * Session duration in milliseconds
   */
  durationMs?: number;
  /**
   * Total trace count in session
   */
  traceCount: number;
  /**
   * Total span count in session
   */
  spanCount: number;
  /**
   * Total error count in session
   */
  errorCount: number;
  /**
   * Service names observed in this session
   */
  services: Array<string>;
  /**
   * Session state
   */
  state: SessionState;
  /**
   * Client information
   */
  client?: SessionClientInfo;
  /**
   * Location information
   */
  geo?: SessionGeoInfo;
  /**
   * GenAI usage summary
   */
  genaiUsage?: SessionGenAiUsage;
}
/**
 * Duration in milliseconds
 */
export type DurationMs = number;
/**
 * Session states
 */
export enum SessionState {
  /**
   * Session is active
   */
  Active = "active",
  /**
   * Session is idle
   */
  Idle = "idle",
  /**
   * Session has ended normally
   */
  Ended = "ended",
  /**
   * Session has timed out
   */
  TimedOut = "timed_out",
  /**
   * Session was invalidated
   */
  Invalidated = "invalidated"
}
/**
 * Session client information
 */
export interface SessionClientInfo {
  /**
   * Client IP address
   */
  ip?: string;
  /**
   * User agent string
   */
  userAgent?: string;
  /**
   * Device type
   */
  deviceType?: DeviceType;
  /**
   * Operating system
   */
  os?: string;
  /**
   * Browser name
   */
  browser?: string;
  /**
   * Browser version
   */
  browserVersion?: string;
}
/**
 * IP address (IPv4 or IPv6)
 */
export type IpAddress = string;
/**
 * User agent string
 */
export type UserAgent = string;
/**
 * Device types
 */
export enum DeviceType {
  /**
   * Desktop computer
   */
  Desktop = "desktop",
  /**
   * Mobile phone
   */
  Mobile = "mobile",
  /**
   * Tablet device
   */
  Tablet = "tablet",
  /**
   * TV or set-top box
   */
  Tv = "tv",
  /**
   * Gaming console
   */
  Console = "console",
  /**
   * Wearable device
   */
  Wearable = "wearable",
  /**
   * IoT device
   */
  Iot = "iot",
  /**
   * Bot/crawler
   */
  Bot = "bot",
  /**
   * Unknown device
   */
  Unknown = "unknown"
}
/**
 * Session geographic information
 */
export interface SessionGeoInfo {
  /**
   * Country code (ISO 3166-1 alpha-2)
   */
  countryCode?: string;
  /**
   * Country name
   */
  countryName?: string;
  /**
   * Region/state
   */
  region?: string;
  /**
   * City
   */
  city?: string;
  /**
   * Postal code
   */
  postalCode?: string;
  /**
   * Timezone
   */
  timezone?: string;
}
/**
 * Session GenAI usage summary
 */
export interface SessionGenAiUsage {
  /**
   * Total GenAI requests in session
   */
  requestCount: number;
  /**
   * Total input tokens consumed
   */
  totalInputTokens: bigint;
  /**
   * Total output tokens generated
   */
  totalOutputTokens: bigint;
  /**
   * Models used in session
   */
  modelsUsed: Array<string>;
  /**
   * Providers used in session
   */
  providersUsed: Array<string>;
  /**
   * Estimated cost in USD
   */
  estimatedCostUsd?: number;
}
/**
 * Aggregated session statistics
 */
export interface SessionStats {
  /**
   * Active sessions count
   */
  activeSessions: bigint;
  /**
   * Total sessions in time range
   */
  totalSessions: bigint;
  /**
   * Unique users in time range
   */
  uniqueUsers: bigint;
  /**
   * Average session duration in milliseconds
   */
  avgDurationMs: number;
  /**
   * Sessions with errors
   */
  sessionsWithErrors: bigint;
  /**
   * Sessions with GenAI usage
   */
  sessionsWithGenAi: bigint;
  /**
   * Bounce rate (single-page sessions)
   */
  bounceRate: number;
  /**
   * Sessions by device type
   */
  byDeviceType?: Array<SessionDeviceStats>;
  /**
   * Sessions by country
   */
  byCountry?: Array<SessionCountryStats>;
}
/**
 * Session stats by device type
 */
export interface SessionDeviceStats {
  /**
   * Device type
   */
  deviceType: DeviceType;
  /**
   * Session count
   */
  count: bigint;
  /**
   * Percentage of total
   */
  percentage: number;
}
/**
 * Session stats by country
 */
export interface SessionCountryStats {
  /**
   * Country code
   */
  countryCode: string;
  /**
   * Country name
   */
  countryName: string;
  /**
   * Session count
   */
  count: bigint;
  /**
   * Percentage of total
   */
  percentage: number;
}
/**
 * Error tracking status
 */
export enum ErrorStatus {
  /**
   * New/unreviewed
   */
  New = "new",
  /**
   * Acknowledged
   */
  Acknowledged = "acknowledged",
  /**
   * In progress
   */
  InProgress = "in_progress",
  /**
   * Resolved
   */
  Resolved = "resolved",
  /**
   * Ignored
   */
  Ignored = "ignored",
  /**
   * Regressed
   */
  Regressed = "regressed",
  /**
   * Won't fix
   */
  WontFix = "wont_fix"
}
/**
 * High-level error categories
 */
export enum ErrorCategory {
  /**
   * Client error (4xx)
   */
  Client = "client",
  /**
   * Server error (5xx)
   */
  Server = "server",
  /**
   * Network error
   */
  Network = "network",
  /**
   * Timeout error
   */
  Timeout = "timeout",
  /**
   * Validation error
   */
  Validation = "validation",
  /**
   * Authentication error
   */
  Authentication = "authentication",
  /**
   * Authorization error
   */
  Authorization = "authorization",
  /**
   * Rate limit error
   */
  RateLimit = "rate_limit",
  /**
   * Resource not found
   */
  NotFound = "not_found",
  /**
   * Conflict error
   */
  Conflict = "conflict",
  /**
   * Internal error
   */
  Internal = "internal",
  /**
   * External service error
   */
  External = "external",
  /**
   * Database error
   */
  Database = "database",
  /**
   * Configuration error
   */
  Configuration = "configuration",
  /**
   * Unknown error
   */
  Unknown = "unknown"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_6 {
  /**
   * List of items in this page
   */
  items: Array<ErrorEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Error entity for tracking and analysis
 */
export interface ErrorEntity {
  /**
   * Error ID
   */
  errorId: string;
  /**
   * Error type (class name or code)
   */
  errorType: string;
  /**
   * Error message
   */
  message: string;
  /**
   * Error category
   */
  category: ErrorCategory;
  /**
   * Fingerprint for grouping
   */
  fingerprint: string;
  /**
   * First occurrence
   */
  firstSeen: Date;
  /**
   * Last occurrence
   */
  lastSeen: Date;
  /**
   * Occurrence count
   */
  occurrenceCount: bigint;
  /**
   * Affected users count
   */
  affectedUsers?: bigint;
  /**
   * Affected services
   */
  affectedServices?: Array<string>;
  /**
   * Status
   */
  status: ErrorStatus;
  /**
   * Assigned to
   */
  assignedTo?: string;
  /**
   * Issue tracker URL
   */
  issueUrl?: string;
  /**
   * Sample trace IDs
   */
  sampleTraces?: Array<string>;
}
/**
 * URL string (absolute)
 */
export type UrlString = string;
/**
 * Error update request
 */
export interface ErrorUpdate {
  /**
   * New status
   */
  status?: ErrorStatus;
  /**
   * Assignee
   */
  assignedTo?: string;
  /**
   * Issue URL
   */
  issueUrl?: string;
}
/**
 * Error statistics
 */
export interface ErrorStats {
  /**
   * Total error count
   */
  totalCount: bigint;
  /**
   * Unique error types
   */
  uniqueTypes: number;
  /**
   * Error rate
   */
  errorRate: number;
  /**
   * Errors by category
   */
  byCategory: Array<ErrorCategoryStats>;
  /**
   * Errors by service
   */
  byService?: Array<ErrorServiceStats>;
  /**
   * Top errors
   */
  topErrors: Array<ErrorTypeStats>;
  /**
   * Trend
   */
  trend: ErrorTrend;
}
/**
 * Error stats by category
 */
export interface ErrorCategoryStats {
  /**
   * Category
   */
  category: ErrorCategory;
  /**
   * Count
   */
  count: bigint;
  /**
   * Percentage of total
   */
  percentage: number;
}
/**
 * Error stats by service
 */
export interface ErrorServiceStats {
  /**
   * Service name
   */
  serviceName: string;
  /**
   * Error count
   */
  count: bigint;
  /**
   * Error rate
   */
  errorRate: number;
  /**
   * Top error type
   */
  topErrorType: string;
}
/**
 * Error stats by type
 */
export interface ErrorTypeStats {
  /**
   * Error type
   */
  errorType: string;
  /**
   * Count
   */
  count: bigint;
  /**
   * Percentage of total
   */
  percentage: number;
  /**
   * Affected users
   */
  affectedUsers?: bigint;
  /**
   * Status
   */
  status: ErrorStatus;
}
/**
 * Error trend
 */
export enum ErrorTrend {
  /**
   * Errors increasing
   */
  Increasing = "increasing",
  /**
   * Errors decreasing
   */
  Decreasing = "decreasing",
  /**
   * Errors stable
   */
  Stable = "stable",
  /**
   * Anomalous spike
   */
  Spike = "spike"
}
/**
 * Error correlation result
 */
export interface ErrorCorrelation {
  /**
   * Error ID
   */
  errorId: string;
  /**
   * Correlated errors
   */
  correlatedErrors: Array<CorrelatedError>;
  /**
   * Potential root cause
   */
  rootCause?: string;
  /**
   * Common attributes
   */
  commonAttributes?: Array<Attribute>;
}
/**
 * Correlated error
 */
export interface CorrelatedError {
  /**
   * Error ID
   */
  errorId: string;
  /**
   * Error type
   */
  errorType: string;
  /**
   * Correlation strength
   */
  correlationStrength: number;
  /**
   * Temporal relationship
   */
  temporalRelationship: TemporalRelationship;
}
/**
 * Temporal relationship between errors
 */
export enum TemporalRelationship {
  /**
   * Errors occur together
   */
  Concurrent = "concurrent",
  /**
   * This error precedes the other
   */
  Precedes = "precedes",
  /**
   * This error follows the other
   */
  Follows = "follows",
  /**
   * No clear temporal pattern
   */
  Unrelated = "unrelated"
}
/**
 * Deployment environments
 */
export enum DeploymentEnvironment {
  /**
   * Development environment
   */
  Development = "development",
  /**
   * Testing environment
   */
  Testing = "testing",
  /**
   * Staging environment
   */
  Staging = "staging",
  /**
   * Production environment
   */
  Production = "production",
  /**
   * Preview/ephemeral environment
   */
  Preview = "preview",
  /**
   * Canary environment
   */
  Canary = "canary"
}
/**
 * Deployment status
 */
export enum DeploymentStatus {
  /**
   * Deployment pending
   */
  Pending = "pending",
  /**
   * Deployment in progress
   */
  InProgress = "in_progress",
  /**
   * Deployment succeeded
   */
  Success = "success",
  /**
   * Deployment failed
   */
  Failed = "failed",
  /**
   * Deployment rolled back
   */
  RolledBack = "rolled_back",
  /**
   * Deployment cancelled
   */
  Cancelled = "cancelled"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_7 {
  /**
   * List of items in this page
   */
  items: Array<DeploymentEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Complete deployment record
 */
export interface DeploymentEntity {
  /**
   * Deployment ID
   */
  deploymentId: string;
  /**
   * Service name
   */
  serviceName: string;
  /**
   * Service version
   */
  serviceVersion: string;
  /**
   * Environment
   */
  environment: DeploymentEnvironment;
  /**
   * Status
   */
  status: DeploymentStatus;
  /**
   * Strategy
   */
  strategy: DeploymentStrategy;
  /**
   * Start time
   */
  startTime: Date;
  /**
   * End time
   */
  endTime?: Date;
  /**
   * Duration in seconds
   */
  durationS?: number;
  /**
   * Deployed by (user/system)
   */
  deployedBy?: string;
  /**
   * Git commit SHA
   */
  gitCommit?: string;
  /**
   * Git branch
   */
  gitBranch?: string;
  /**
   * Previous version
   */
  previousVersion?: string;
  /**
   * Rollback target (if rolled back)
   */
  rollbackTarget?: string;
  /**
   * Replica count
   */
  replicaCount?: number;
  /**
   * Healthy replica count
   */
  healthyReplicas?: number;
  /**
   * Error message (if failed)
   */
  errorMessage?: string;
}
/**
 * Deployment strategies
 */
export enum DeploymentStrategy {
  /**
   * Rolling update
   */
  Rolling = "rolling",
  /**
   * Blue-green deployment
   */
  BlueGreen = "blue_green",
  /**
   * Canary deployment
   */
  Canary = "canary",
  /**
   * Recreate (stop old, start new)
   */
  Recreate = "recreate",
  /**
   * A/B testing deployment
   */
  AbTest = "ab_test",
  /**
   * Shadow deployment
   */
  Shadow = "shadow",
  /**
   * Feature flag controlled
   */
  FeatureFlag = "feature_flag"
}
/**
 * Duration in seconds
 */
export type DurationS = number;
/**
 * Deployment creation request
 */
export interface DeploymentCreate {
  /**
   * Service name
   */
  serviceName: string;
  /**
   * Service version
   */
  serviceVersion: string;
  /**
   * Environment
   */
  environment: DeploymentEnvironment;
  /**
   * Strategy
   */
  strategy: DeploymentStrategy;
  /**
   * Deployed by
   */
  deployedBy?: string;
  /**
   * Git commit SHA
   */
  gitCommit?: string;
  /**
   * Git branch
   */
  gitBranch?: string;
}
/**
 * Deployment update request
 */
export interface DeploymentUpdate {
  /**
   * New status
   */
  status?: DeploymentStatus;
  /**
   * Healthy replicas
   */
  healthyReplicas?: number;
  /**
   * Error message
   */
  errorMessage?: string;
}
/**
 * DORA metrics response
 */
export interface DoraMetrics {
  /**
   * Deployment frequency (per day)
   */
  deploymentFrequency: number;
  /**
   * Lead time for changes (hours)
   */
  leadTimeHours: number;
  /**
   * Change failure rate
   */
  changeFailureRate: number;
  /**
   * Mean time to recovery (hours)
   */
  mttrHours: number;
  /**
   * Performance level
   */
  performanceLevel: DoraPerformanceLevel;
}
/**
 * DORA performance levels
 */
export enum DoraPerformanceLevel {
  /**
   * Elite performer
   */
  Elite = "elite",
  /**
   * High performer
   */
  High = "high",
  /**
   * Medium performer
   */
  Medium = "medium",
  /**
   * Low performer
   */
  Low = "low"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_8 {
  /**
   * List of items in this page
   */
  items: Array<ServiceInfo>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Service information
 */
export interface ServiceInfo {
  /**
   * Service name
   */
  name: string;
  /**
   * Service namespace
   */
  namespaceName?: string;
  /**
   * Service version
   */
  version?: string;
  /**
   * Instance count
   */
  instanceCount: number;
  /**
   * Last seen
   */
  lastSeen: Date;
}
/**
 * Service details
 */
export interface ServiceDetails {
  /**
   * Service name
   */
  name: string;
  /**
   * Service namespace
   */
  namespaceName?: string;
  /**
   * Service version
   */
  version?: string;
  /**
   * Instance count
   */
  instanceCount: number;
  /**
   * Last seen
   */
  lastSeen: Date;
  /**
   * Resource attributes
   */
  resourceAttributes: Array<Attribute>;
  /**
   * Instrumentation libraries
   */
  instrumentationLibraries: Array<InstrumentationScope>;
  /**
   * Request rate (per second)
   */
  requestRate: number;
  /**
   * Error rate
   */
  errorRate: number;
  /**
   * Average latency in milliseconds
   */
  avgLatencyMs: number;
  /**
   * P99 latency in milliseconds
   */
  p99LatencyMs: number;
}
/**
 * Service dependency map
 */
export interface ServiceDependency {
  /**
   * Source service
   */
  sourceService: string;
  /**
   * Target service
   */
  targetService: string;
  /**
   * Request count
   */
  requestCount: bigint;
  /**
   * Error rate
   */
  errorRate: number;
  /**
   * Average latency in milliseconds
   */
  avgLatencyMs: number;
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_9 {
  /**
   * List of items in this page
   */
  items: Array<OperationInfo>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Operation information
 */
export interface OperationInfo {
  /**
   * Operation name
   */
  name: string;
  /**
   * Span kind
   */
  spanKind: SpanKind;
  /**
   * Request count
   */
  requestCount: bigint;
  /**
   * Error count
   */
  errorCount: bigint;
  /**
   * Average duration in milliseconds
   */
  avgDurationMs: number;
  /**
   * P99 duration in milliseconds
   */
  p99DurationMs: number;
}
/**
 * Workspace envelope: the local-first workspace unit
 */
export interface WorkspaceEnvelopeEntity {
  /**
   * Workspace ID
   */
  id: string;
  /**
   * Owning project
   */
  projectId: string;
  /**
   * Environment
   */
  environmentId: string;
  /**
   * Host node
   */
  nodeId: string;
  /**
   * Workspace name
   */
  name: string;
  /**
   * Local filesystem root path
   */
  rootPath: string;
  /**
   * Last heartbeat timestamp
   */
  heartbeatAt?: Date;
  /**
   * Heartbeat interval in seconds
   */
  heartbeatIntervalSeconds: number;
  /**
   * Workspace status
   */
  status: WorkspaceStatus;
  /**
   * Workspace-level configuration overrides
   */
  configJson?: string;
  /**
   * Creation timestamp
   */
  createdAt: Date;
  /**
   * Last update timestamp
   */
  updatedAt: Date;
}
/**
 * Workspace lifecycle status
 */
export enum WorkspaceStatus {
  /**
   * Active and receiving data
   */
  Active = "active",
  /**
   * Temporarily suspended
   */
  Suspended = "suspended",
  /**
   * Archived (read-only)
   */
  Archived = "archived"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_10 {
  /**
   * List of items in this page
   */
  items: Array<ProjectEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Project registry: top-level organizational unit
 */
export interface ProjectEntity {
  /**
   * Project ID
   */
  id: string;
  /**
   * Project name
   */
  name: string;
  /**
   * URL-safe slug (unique)
   */
  slug: string;
  /**
   * Project description
   */
  description?: string;
  /**
   * Creation timestamp
   */
  createdAt: Date;
  /**
   * Last update timestamp
   */
  updatedAt: Date;
  /**
   * Archive timestamp (null if active)
   */
  archivedAt?: Date;
}
/**
 * Project creation request
 */
export interface ProjectCreateRequest {
  /**
   * Project name
   */
  name: string;
  /**
   * Project slug (URL-safe)
   */
  slug: string;
  /**
   * Project description
   */
  description?: string;
}
/**
 * Environment row per project (dev, staging, prod)
 */
export interface ProjectEnvironmentEntity {
  /**
   * Environment ID
   */
  id: string;
  /**
   * Owning project
   */
  projectId: string;
  /**
   * Environment name (dev, staging, prod)
   */
  name: string;
  /**
   * Display name for UI
   */
  displayName: string;
  /**
   * Hex color for UI
   */
  color?: string;
  /**
   * Sort order for display
   */
  sortOrder: number;
  /**
   * Creation timestamp
   */
  createdAt: Date;
}
/**
 * Handshake start request
 */
export interface HandshakeStartRequest {
  /**
   * PKCE code challenge
   */
  codeChallenge: string;
  /**
   * Client identifier
   */
  clientId: string;
}
/**
 * Browser-local handshake session for workspace verification
 */
export interface HandshakeSessionEntity {
  /**
   * Session ID
   */
  id: string;
  /**
   * Target workspace
   */
  workspaceId: string;
  /**
   * PKCE-style challenge
   */
  challenge: string;
  /**
   * Challenge method
   */
  challengeMethod: string;
  /**
   * Browser fingerprint
   */
  browserFingerprint?: string;
  /**
   * Origin URL
   */
  originUrl?: string;
  /**
   * Handshake state
   */
  state: HandshakeState;
  /**
   * Verification timestamp
   */
  verifiedAt?: Date;
  /**
   * Expiration timestamp
   */
  expiresAt: Date;
  /**
   * Creation timestamp
   */
  createdAt: Date;
}
/**
 * Handshake session state
 */
export enum HandshakeState {
  /**
   * Waiting for verification
   */
  Pending = "pending",
  /**
   * Successfully verified
   */
  Verified = "verified",
  /**
   * Session expired
   */
  Expired = "expired",
  /**
   * Verification rejected
   */
  Rejected = "rejected"
}
/**
 * Handshake verification request
 */
export interface HandshakeVerifyRequest {
  /**
   * PKCE code verifier
   */
  codeVerifier: string;
  /**
   * Authorization code
   */
  code: string;
}
/**
 * Handshake verification response
 */
export interface HandshakeVerifyResponse {
  /**
   * Access token
   */
  accessToken: string;
  /**
   * Token expiration
   */
  expiresAt: Date;
  /**
   * Workspace ID
   */
  workspaceId: string;
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_11 {
  /**
   * List of items in this page
   */
  items: Array<GenerationProfileEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Named instrumentation profile for code generation
 */
export interface GenerationProfileEntity {
  /**
   * Profile ID
   */
  id: string;
  /**
   * Owning project
   */
  projectId: string;
  /**
   * Profile name
   */
  name: string;
  /**
   * Profile description
   */
  description?: string;
  /**
   * Target framework (e.g. net10.0)
   */
  targetFramework: string;
  /**
   * Target language
   */
  targetLanguage: string;
  /**
   * Semantic conventions version
   */
  semconvVersion: string;
  /**
   * Enabled features/modules
   */
  featuresJson: string;
  /**
   * Template customizations
   */
  templateOverridesJson?: string;
  /**
   * Whether this is the default profile
   */
  isDefault: boolean;
  /**
   * Creation timestamp
   */
  createdAt: Date;
  /**
   * Last update timestamp
   */
  updatedAt: Date;
}
/**
 * Generation profile creation request
 */
export interface GenerationProfileCreateRequest {
  /**
   * Profile name
   */
  name: string;
  /**
   * Target framework
   */
  targetFramework: string;
  /**
   * Profile description
   */
  description?: string;
  /**
   * Feature flags
   */
  featuresJson?: string;
}
/**
 * Selected semconv/feature per workspace for code generation
 */
export interface GenerationSelectionEntity {
  /**
   * Selection ID
   */
  id: string;
  /**
   * Workspace
   */
  workspaceId: string;
  /**
   * Profile
   */
  profileId: string;
  /**
   * Selection type (semconv_group, feature, custom_attribute)
   */
  selectionType: string;
  /**
   * Selection key (e.g. http, db, genai)
   */
  selectionKey: string;
  /**
   * Whether enabled
   */
  enabled: boolean;
  /**
   * Selection-specific configuration
   */
  configJson?: string;
  /**
   * Creation timestamp
   */
  createdAt: Date;
  /**
   * Last update timestamp
   */
  updatedAt: Date;
}
/**
 * Save generation selections request
 */
export interface GenerationSelectionSaveRequest {
  /**
   * Workspace ID
   */
  workspaceId: string;
  /**
   * Profile ID
   */
  profileId: string;
  /**
   * Selected semconv keys
   */
  selectedKeysJson: string;
}
/**
 * Generation job creation request
 */
export interface GenerationJobCreateRequest {
  /**
   * Workspace ID
   */
  workspaceId: string;
  /**
   * Profile ID
   */
  profileId: string;
  /**
   * Job type
   */
  jobType: GenerationJobType;
}
/**
 * Generation job types
 */
export enum GenerationJobType {
  /**
   * Full regeneration
   */
  Full = "full",
  /**
   * Incremental update
   */
  Incremental = "incremental",
  /**
   * Preview/dry-run
   */
  Preview = "preview"
}
/**
 * Code generation job entry
 */
export interface GenerationJobEntity {
  /**
   * Job ID
   */
  id: string;
  /**
   * Workspace
   */
  workspaceId: string;
  /**
   * Profile
   */
  profileId: string;
  /**
   * Job type
   */
  jobType: GenerationJobType;
  /**
   * Job status
   */
  status: JobStatus;
  /**
   * Priority (higher = more urgent)
   */
  priority: number;
  /**
   * Hash of inputs for dedup
   */
  inputHash?: string;
  /**
   * Local path where output was written
   */
  outputPath?: string;
  /**
   * Hash of generated output
   */
  outputHash?: string;
  /**
   * Error message if failed
   */
  errorMessage?: string;
  /**
   * Queue timestamp
   */
  queuedAt: Date;
  /**
   * Start timestamp
   */
  startedAt?: Date;
  /**
   * Completion timestamp
   */
  completedAt?: Date;
  /**
   * Duration in milliseconds
   */
  durationMs?: number;
}
/**
 * Job lifecycle status
 */
export enum JobStatus {
  /**
   * Queued for execution
   */
  Queued = "queued",
  /**
   * Currently executing
   */
  Running = "running",
  /**
   * Successfully completed
   */
  Completed = "completed",
  /**
   * Failed with error
   */
  Failed = "failed",
  /**
   * Cancelled by user
   */
  Cancelled = "cancelled"
}
/**
 * Issue lifecycle status
 */
export enum IssueStatus {
  /**
   * Unresolved/new
   */
  Unresolved = "unresolved",
  /**
   * Acknowledged by team
   */
  Acknowledged = "acknowledged",
  /**
   * Being investigated
   */
  Investigating = "investigating",
  /**
   * Fix in progress
   */
  InProgress = "in_progress",
  /**
   * Resolved
   */
  Resolved = "resolved",
  /**
   * Ignored
   */
  Ignored = "ignored",
  /**
   * Regressed after resolution
   */
  Regressed = "regressed"
}
/**
 * Issue priority
 */
export enum IssuePriority {
  /**
   * Critical priority
   */
  Critical = "critical",
  /**
   * High priority
   */
  High = "high",
  /**
   * Medium priority
   */
  Medium = "medium",
  /**
   * Low priority
   */
  Low = "low"
}
/**
 * Issue severity level
 */
export enum IssueLevel {
  /**
   * Debug level
   */
  Debug = "debug",
  /**
   * Info level
   */
  Info = "info",
  /**
   * Warning level
   */
  Warning = "warning",
  /**
   * Error level
   */
  Error = "error",
  /**
   * Fatal/critical level
   */
  Fatal = "fatal"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_12 {
  /**
   * List of items in this page
   */
  items: Array<ErrorIssueEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Error issue aggregate with lifecycle tracking
 */
export interface ErrorIssueEntity {
  /**
   * Issue ID
   */
  id: string;
  /**
   * Owning project
   */
  projectId: string;
  /**
   * Error fingerprint for grouping
   */
  fingerprint: string;
  /**
   * Issue title
   */
  title: string;
  /**
   * Culprit (function/module causing the error)
   */
  culprit?: string;
  /**
   * Error type (exception class name or code)
   */
  errorType: string;
  /**
   * Error category
   */
  category: string;
  /**
   * Severity level
   */
  level: IssueLevel;
  /**
   * Platform (csharp, javascript, python, etc.)
   */
  platform?: string;
  /**
   * First occurrence
   */
  firstSeenAt: Date;
  /**
   * Last occurrence
   */
  lastSeenAt: Date;
  /**
   * Total occurrence count
   */
  occurrenceCount: bigint;
  /**
   * Affected unique users count
   */
  affectedUsersCount: number;
  /**
   * Issue status
   */
  status: IssueStatus;
  /**
   * Issue substatus
   */
  substatus?: string;
  /**
   * Priority level
   */
  priority: IssuePriority;
  /**
   * Assigned team member
   */
  assignedTo?: string;
  /**
   * Resolution timestamp
   */
  resolvedAt?: Date;
  /**
   * Resolved by
   */
  resolvedBy?: string;
  /**
   * Number of regressions
   */
  regressionCount: number;
  /**
   * Last associated release
   */
  lastRelease?: string;
  /**
   * Issue tags
   */
  tagsJson?: string;
  /**
   * Issue metadata
   */
  metadataJson?: string;
  /**
   * Creation timestamp
   */
  createdAt: Date;
  /**
   * Last update timestamp
   */
  updatedAt: Date;
}
/**
 * Issue update request
 */
export interface IssueUpdateRequest {
  /**
   * New status
   */
  status?: IssueStatus;
  /**
   * New priority
   */
  priority?: IssuePriority;
  /**
   * Assignee
   */
  assignedTo?: string;
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_13 {
  /**
   * List of items in this page
   */
  items: Array<ErrorIssueEventEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Individual error occurrence linked to an issue
 */
export interface ErrorIssueEventEntity {
  /**
   * Event ID
   */
  id: string;
  /**
   * Parent issue
   */
  issueId: string;
  /**
   * Associated trace ID
   */
  traceId?: string;
  /**
   * Associated span ID
   */
  spanId?: string;
  /**
   * Error message
   */
  message?: string;
  /**
   * Stack trace
   */
  stackTrace?: string;
  /**
   * Parsed stack frames
   */
  stackFramesJson?: string;
  /**
   * Environment (dev, staging, prod)
   */
  environment?: string;
  /**
   * Release version
   */
  releaseVersion?: string;
  /**
   * Affected user ID
   */
  userId?: string;
  /**
   * Client IP address
   */
  userIp?: string;
  /**
   * Request URL
   */
  requestUrl?: string;
  /**
   * HTTP request method
   */
  requestMethod?: string;
  /**
   * Browser info
   */
  browser?: string;
  /**
   * Operating system
   */
  os?: string;
  /**
   * Device info
   */
  device?: string;
  /**
   * Runtime name
   */
  runtime?: string;
  /**
   * Runtime version
   */
  runtimeVersion?: string;
  /**
   * Additional context data
   */
  contextJson?: string;
  /**
   * Event tags
   */
  tagsJson?: string;
  /**
   * Event timestamp
   */
  timestamp: Date;
}
/**
 * Pre-error context breadcrumb
 */
export interface ErrorBreadcrumbEntity {
  /**
   * Breadcrumb ID
   */
  id: string;
  /**
   * Parent event
   */
  eventId: string;
  /**
   * Breadcrumb type
   */
  breadcrumbType: BreadcrumbType;
  /**
   * Category (e.g. http, db, ui)
   */
  category?: string;
  /**
   * Breadcrumb message
   */
  message?: string;
  /**
   * Severity level
   */
  level: string;
  /**
   * Breadcrumb data
   */
  dataJson?: string;
  /**
   * Breadcrumb timestamp
   */
  timestamp: Date;
}
/**
 * Breadcrumb types
 */
export enum BreadcrumbType {
  /**
   * Navigation event
   */
  Navigation = "navigation",
  /**
   * HTTP request
   */
  Http = "http",
  /**
   * Database query
   */
  Query = "query",
  /**
   * User interaction
   */
  User = "user",
  /**
   * Log message
   */
  Log = "log",
  /**
   * Error occurrence
   */
  Error = "error",
  /**
   * Debug information
   */
  Debug = "debug",
  /**
   * Default/other
   */
  Default = "default"
}
/**
 * Workflow run status
 */
export enum WorkflowRunStatus {
  /**
   * Pending start
   */
  Pending = "pending",
  /**
   * Currently running
   */
  Running = "running",
  /**
   * Paused/waiting for input
   */
  Paused = "paused",
  /**
   * Successfully completed
   */
  Completed = "completed",
  /**
   * Failed with error
   */
  Failed = "failed",
  /**
   * Cancelled
   */
  Cancelled = "cancelled",
  /**
   * Timed out
   */
  TimedOut = "timed_out"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_14 {
  /**
   * List of items in this page
   */
  items: Array<WorkflowRunEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Top-level workflow execution
 */
export interface WorkflowRunEntity {
  /**
   * Run ID
   */
  id: string;
  /**
   * Workflow definition ID
   */
  workflowId: string;
  /**
   * Workflow definition version
   */
  workflowVersion: number;
  /**
   * Owning project
   */
  projectId: string;
  /**
   * Trigger type
   */
  triggerType: WorkflowTriggerType;
  /**
   * Trigger source identifier
   */
  triggerSource?: string;
  /**
   * Run input data
   */
  inputJson?: string;
  /**
   * Run output data
   */
  outputJson?: string;
  /**
   * Run status
   */
  status: WorkflowRunStatus;
  /**
   * Error message if failed
   */
  errorMessage?: string;
  /**
   * Parent run ID for sub-workflows
   */
  parentRunId?: string;
  /**
   * Correlation ID for tracing
   */
  correlationId?: string;
  /**
   * Start timestamp
   */
  startedAt?: Date;
  /**
   * Completion timestamp
   */
  completedAt?: Date;
  /**
   * Duration in milliseconds
   */
  durationMs?: number;
  /**
   * Creation timestamp
   */
  createdAt: Date;
}
/**
 * Workflow trigger types
 */
export enum WorkflowTriggerType {
  /**
   * Manual trigger
   */
  Manual = "manual",
  /**
   * Alert-triggered
   */
  Alert = "alert",
  /**
   * Schedule-triggered
   */
  Schedule = "schedule",
  /**
   * Event-triggered
   */
  Event = "event",
  /**
   * API-triggered
   */
  Api = "api",
  /**
   * MCP tool-triggered
   */
  Mcp = "mcp"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_15 {
  /**
   * List of items in this page
   */
  items: Array<WorkflowNodeEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Individual DAG node execution
 */
export interface WorkflowNodeEntity {
  /**
   * Execution ID
   */
  id: string;
  /**
   * Parent run
   */
  runId: string;
  /**
   * Node definition ID
   */
  nodeId: string;
  /**
   * Node type
   */
  nodeType: WorkflowNodeType;
  /**
   * Node name
   */
  nodeName: string;
  /**
   * Attempt number (1-based)
   */
  attempt: number;
  /**
   * Node input data
   */
  inputJson?: string;
  /**
   * Node output data
   */
  outputJson?: string;
  /**
   * Node status
   */
  status: WorkflowRunStatus;
  /**
   * Error message if failed
   */
  errorMessage?: string;
  /**
   * Retry count
   */
  retryCount: number;
  /**
   * Maximum retries allowed
   */
  maxRetries: number;
  /**
   * Timeout in milliseconds
   */
  timeoutMs?: number;
  /**
   * Start timestamp
   */
  startedAt?: Date;
  /**
   * Completion timestamp
   */
  completedAt?: Date;
  /**
   * Duration in milliseconds
   */
  durationMs?: number;
  /**
   * Creation timestamp
   */
  createdAt: Date;
}
/**
 * Workflow node types
 */
export enum WorkflowNodeType {
  /**
   * AI agent invocation
   */
  Agent = "agent",
  /**
   * Tool/function call
   */
  Tool = "tool",
  /**
   * Conditional branch
   */
  Condition = "condition",
  /**
   * Parallel fork
   */
  Fork = "fork",
  /**
   * Join/barrier
   */
  Join = "join",
  /**
   * Human approval gate
   */
  Approval = "approval",
  /**
   * Sub-workflow invocation
   */
  SubWorkflow = "sub_workflow",
  /**
   * Transform/map node
   */
  Transform = "transform",
  /**
   * Wait/delay node
   */
  Wait = "wait"
}
/**
 * Append-only workflow event
 */
export interface WorkflowEventEntity {
  /**
   * Event ID
   */
  id: string;
  /**
   * Parent run
   */
  runId: string;
  /**
   * Source node (null for run-level events)
   */
  nodeId?: string;
  /**
   * Event type
   */
  eventType: string;
  /**
   * Event name
   */
  eventName: string;
  /**
   * Event payload
   */
  payloadJson?: string;
  /**
   * Monotonic sequence number
   */
  sequenceNumber: bigint;
  /**
   * Event source identifier
   */
  source?: string;
  /**
   * Correlation ID
   */
  correlationId?: string;
  /**
   * Event timestamp
   */
  timestamp: Date;
}
/**
 * Unified search request
 */
export interface SearchRequest {
  /**
   * Search query text
   */
  query: string;
  /**
   * Entity type filters
   */
  entityTypes?: Array<SearchEntityType>;
  /**
   * Project scope
   */
  projectId?: string;
  /**
   * Maximum results
   */
  limit?: number;
  /**
   * Cursor for pagination
   */
  cursor?: string;
}
/**
 * Searchable entity types
 */
export enum SearchEntityType {
  /**
   * Trace/span
   */
  Span = "span",
  /**
   * Log entry
   */
  Log = "log",
  /**
   * Error issue
   */
  Issue = "issue",
  /**
   * Workflow run
   */
  Workflow = "workflow",
  /**
   * Deployment
   */
  Deployment = "deployment",
  /**
   * Session
   */
  Session = "session",
  /**
   * Alert
   */
  Alert = "alert",
  /**
   * Fix run
   */
  Fix = "fix"
}
/**
 * Unified search response
 */
export interface SearchResponse {
  /**
   * Search results
   */
  results: Array<SearchResult>;
  /**
   * Total matching documents
   */
  totalCount: bigint;
  /**
   * Query execution time in ms
   */
  durationMs: number;
  /**
   * Next page cursor
   */
  nextCursor?: string;
  /**
   * Search suggestions
   */
  suggestions?: Array<string>;
}
/**
 * Individual search result
 */
export interface SearchResult {
  /**
   * Document ID
   */
  documentId: string;
  /**
   * Entity type
   */
  entityType: SearchEntityType;
  /**
   * Entity ID
   */
  entityId: string;
  /**
   * Result title
   */
  title: string;
  /**
   * Result snippet with highlights
   */
  snippet?: string;
  /**
   * Relevance score
   */
  score: number;
  /**
   * Link to entity
   */
  url?: string;
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_16 {
  /**
   * List of items in this page
   */
  items: Array<AlertRuleEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Alert rule definition
 */
export interface AlertRuleEntity {
  /**
   * Rule ID
   */
  id: string;
  /**
   * Owning project
   */
  projectId: string;
  /**
   * Rule name
   */
  name: string;
  /**
   * Rule description
   */
  description?: string;
  /**
   * Rule type
   */
  ruleType: AlertRuleType;
  /**
   * Condition definition
   */
  conditionJson: string;
  /**
   * Threshold definition
   */
  thresholdJson?: string;
  /**
   * Target type for evaluation
   */
  targetType: string;
  /**
   * Target filter
   */
  targetFilterJson?: string;
  /**
   * Alert severity
   */
  severity: AlertSeverity;
  /**
   * Cooldown between firings in seconds
   */
  cooldownSeconds: number;
  /**
   * Notification channels
   */
  notificationChannelsJson?: string;
  /**
   * Whether rule is enabled
   */
  enabled: boolean;
  /**
   * Last trigger timestamp
   */
  lastTriggeredAt?: Date;
  /**
   * Total trigger count
   */
  triggerCount: bigint;
  /**
   * Creation timestamp
   */
  createdAt: Date;
  /**
   * Last update timestamp
   */
  updatedAt: Date;
}
/**
 * Alert rule types
 */
export enum AlertRuleType {
  /**
   * Metric threshold alert
   */
  Threshold = "threshold",
  /**
   * Error rate alert
   */
  ErrorRate = "error_rate",
  /**
   * New issue alert
   */
  NewIssue = "new_issue",
  /**
   * Regression alert
   */
  Regression = "regression",
  /**
   * SLO burn rate alert
   */
  BurnRate = "burn_rate",
  /**
   * Anomaly detection alert
   */
  Anomaly = "anomaly",
  /**
   * Custom query alert
   */
  Custom = "custom"
}
/**
 * Alert severity levels
 */
export enum AlertSeverity {
  /**
   * Critical alert
   */
  Critical = "critical",
  /**
   * Warning alert
   */
  Warning = "warning",
  /**
   * Informational alert
   */
  Info = "info"
}
/**
 * Alert firing status
 */
export enum AlertFiringStatus {
  /**
   * Currently firing
   */
  Firing = "firing",
  /**
   * Acknowledged by operator
   */
  Acknowledged = "acknowledged",
  /**
   * Resolved (condition cleared)
   */
  Resolved = "resolved",
  /**
   * Suppressed by cooldown
   */
  Suppressed = "suppressed"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_17 {
  /**
   * List of items in this page
   */
  items: Array<AlertFiringEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * Triggered alert instance
 */
export interface AlertFiringEntity {
  /**
   * Firing ID
   */
  id: string;
  /**
   * Source rule
   */
  ruleId: string;
  /**
   * Dedup fingerprint
   */
  fingerprint: string;
  /**
   * Alert severity
   */
  severity: AlertSeverity;
  /**
   * Alert title
   */
  title: string;
  /**
   * Alert message
   */
  message?: string;
  /**
   * Measured value that triggered the alert
   */
  triggerValue?: number;
  /**
   * Threshold value
   */
  thresholdValue?: number;
  /**
   * Additional context
   */
  contextJson?: string;
  /**
   * Firing status
   */
  status: AlertFiringStatus;
  /**
   * Acknowledgment timestamp
   */
  acknowledgedAt?: Date;
  /**
   * Acknowledged by
   */
  acknowledgedBy?: string;
  /**
   * Resolution timestamp
   */
  resolvedAt?: Date;
  /**
   * Firing timestamp
   */
  firedAt: Date;
  /**
   * Dedup key for suppressing duplicates
   */
  dedupKey?: string;
}
/**
 * Fix run status
 */
export enum FixRunStatus {
  /**
   * Pending execution
   */
  Pending = "pending",
  /**
   * Running
   */
  Running = "running",
  /**
   * Awaiting approval
   */
  AwaitingApproval = "awaiting_approval",
  /**
   * Approved and applied
   */
  Applied = "applied",
  /**
   * Rejected
   */
  Rejected = "rejected",
  /**
   * Failed
   */
  Failed = "failed"
}
/**
 * Cursor-based paginated response wrapper
 */
export interface CursorPage_18 {
  /**
   * List of items in this page
   */
  items: Array<FixRunEntity>;
  /**
   * Cursor for the next page (null if no more pages)
   */
  nextCursor?: string;
  /**
   * Cursor for the previous page (null if first page)
   */
  prevCursor?: string;
  /**
   * Whether there are more items available
   */
  hasMore: boolean;
}
/**
 * AI-assisted fix attempt
 */
export interface FixRunEntity {
  /**
   * Fix run ID
   */
  id: string;
  /**
   * Target issue
   */
  issueId: string;
  /**
   * Triggering alert firing
   */
  alertFiringId?: string;
  /**
   * What triggered the fix
   */
  triggerType: FixTriggerType;
  /**
   * Fix strategy
   */
  strategy: string;
  /**
   * AI model used
   */
  modelName?: string;
  /**
   * AI provider
   */
  modelProvider?: string;
  /**
   * Fix run status
   */
  status: FixRunStatus;
  /**
   * Error message if failed
   */
  errorMessage?: string;
  /**
   * Tokens consumed
   */
  tokensUsed?: number;
  /**
   * Duration in milliseconds
   */
  durationMs?: number;
  /**
   * Creation timestamp
   */
  createdAt: Date;
  /**
   * Start timestamp
   */
  startedAt?: Date;
  /**
   * Completion timestamp
   */
  completedAt?: Date;
}
/**
 * Fix trigger types
 */
export enum FixTriggerType {
  /**
   * Triggered by alert
   */
  Alert = "alert",
  /**
   * Triggered manually
   */
  Manual = "manual",
  /**
   * Triggered by MCP tool
   */
  Mcp = "mcp",
  /**
   * Triggered by schedule
   */
  Scheduled = "scheduled"
}
