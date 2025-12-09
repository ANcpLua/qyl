// qyl. Telemetry Types
// Based on OpenTelemetry semantic conventions 1.38

export type SpanKind = 'internal' | 'server' | 'client' | 'producer' | 'consumer';

export type SpanStatus = 'unset' | 'ok' | 'error';

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';

export type ResourceStatus = 'running' | 'stopped' | 'error' | 'pending' | 'starting';

export interface Attributes {
  [key: string]: string | number | boolean | string[] | number[] | boolean[];
}

export interface SpanContext {
  traceId: string;
  spanId: string;
  parentSpanId?: string;
  traceFlags: number;
}

export interface Span {
  traceId: string;
  spanId: string;
  parentSpanId?: string;
  name: string;
  kind: SpanKind;
  status: SpanStatus;
  statusMessage?: string;
  startTime: string; // ISO timestamp
  endTime: string;   // ISO timestamp
  durationMs: number;
  attributes: Attributes;
  events: SpanEvent[];
  links: SpanLink[];

  // Resource info
  serviceName: string;
  serviceVersion?: string;

  // GenAI specific (semconv 1.38)
  genai?: GenAISpanData;
}

export interface SpanEvent {
  name: string;
  timestamp: string;
  attributes: Attributes;
}

export interface SpanLink {
  traceId: string;
  spanId: string;
  attributes: Attributes;
}

export interface GenAISpanData {
  // Core attributes (semconv 1.38)
  providerName: string;           // gen_ai.provider.name
  requestModel: string;           // gen_ai.request.model
  responseModel?: string;         // gen_ai.response.model
  operationName: string;          // gen_ai.operation.name

  // Token usage (semconv 1.38 naming)
  inputTokens?: number;           // gen_ai.usage.input_tokens
  outputTokens?: number;          // gen_ai.usage.output_tokens

  // Computed/derived fields (not in semconv, calculated by collector)
  totalTokens?: number;           // inputTokens + outputTokens
  costUsd?: number;               // Calculated from token counts + model pricing

  // Response metadata
  finishReasons?: string[];       // gen_ai.response.finish_reasons

  // Message content
  inputMessages?: GenAIMessage[]; // gen_ai.input.messages
  outputMessages?: GenAIMessage[];// gen_ai.output.messages
  toolCalls?: GenAIToolCall[];
}

export interface GenAIMessage {
  role: string;
  content?: string;
  toolCalls?: GenAIToolCall[];
}

export interface GenAIToolCall {
  id: string;
  type: string;
  function: {
    name: string;
    arguments: string;
  };
}

export interface LogRecord {
  timestamp: string;
  observedTimestamp: string;
  traceId?: string;
  spanId?: string;
  severityNumber: number;
  severityText: LogLevel;
  body: string;
  attributes: Attributes;

  // Resource info
  serviceName: string;
  serviceVersion?: string;
}

export interface MetricDataPoint {
  timestamp: string;
  value: number;
  attributes?: Attributes;
}

export interface Metric {
  name: string;
  description?: string;
  unit?: string;
  type: 'gauge' | 'counter' | 'histogram' | 'summary';
  dataPoints: MetricDataPoint[];

  // Resource info
  serviceName: string;
}

export interface Resource {
  id: string;
  name: string;
  type: 'service' | 'container' | 'process' | 'host';
  status: ResourceStatus;
  startTime?: string;
  attributes: Attributes;

  // Stats
  spanCount?: number;
  logCount?: number;
  errorRate?: number;
  latencyP50?: number;
  latencyP99?: number;
}

export interface Session {
  sessionId: string;
  serviceName: string;
  startTime: string;
  endTime?: string;
  spanCount: number;
  errorCount: number;
  attributes: Attributes;

  // GenAI summary
  genaiStats?: {
    totalTokensIn: number;
    totalTokensOut: number;
    totalCostUsd: number;
    providers: string[];
    models: string[];
  };
}

export interface Trace {
  traceId: string;
  rootSpan: Span;
  spans: Span[];
  serviceName: string;
  startTime: string;
  durationMs: number;
  spanCount: number;
  errorCount: number;
  services: string[];
}

// API Response types
export interface SessionsResponse {
  sessions: Session[];
  total: number;
  hasMore: boolean;
}

export interface SpansResponse {
  spans: Span[];
  total: number;
  hasMore: boolean;
}

export interface TraceResponse {
  spans: Span[];
}

export interface LogsResponse {
  logs: LogRecord[];
  total: number;
  hasMore: boolean;
}

export interface MetricsResponse {
  metrics: Metric[];
}

export interface ResourcesResponse {
  resources: Resource[];
}

// SSE Event types
export interface SpanBatch {
  spans: Span[];
  sessionId?: string;
  timestamp: string;
}

export interface SSEConnectedEvent {
  connectionId: string;
}

// Filter types
export interface TimeRange {
  start: string;
  end: string;
}

export interface SpanFilter {
  serviceName?: string;
  operationName?: string;
  status?: SpanStatus;
  minDuration?: number;
  maxDuration?: number;
  timeRange?: TimeRange;
  attributes?: Attributes;
}

export interface LogFilter {
  serviceName?: string;
  minLevel?: LogLevel;
  search?: string;
  traceId?: string;
  timeRange?: TimeRange;
}

// Keyboard shortcuts
export interface KeyboardShortcut {
  key: string;
  modifiers?: ('ctrl' | 'alt' | 'shift' | 'meta')[];
  description: string;
  action: () => void;
}
