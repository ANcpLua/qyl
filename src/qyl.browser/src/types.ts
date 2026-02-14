// ---------------------------------------------------------------------------
// OTLP wire format types (generated from TypeSpec source of truth)
// ---------------------------------------------------------------------------
export type {
  OtlpAnyValue,
  OtlpAttribute,
  OtlpInstrumentationScope,
  OtlpResource,
  OtlpSpan,
  OtlpSpanStatus,
  OtlpLogRecord,
  OtlpScopeSpans,
  OtlpScopeLogs,
  OtlpResourceSpans,
  OtlpResourceLogs,
  ExportTraceServiceRequest,
  ExportLogsServiceRequest,
} from './generated/otlp-types.js';

import type { OtlpSpan, OtlpLogRecord } from './generated/otlp-types.js';

// ---------------------------------------------------------------------------
// SDK configuration types (hand-authored, SDK-specific)
// ---------------------------------------------------------------------------

/** Configuration for the qyl browser SDK. */
export interface QylConfig {
  /** Collector URL (e.g., 'http://localhost:5100'). */
  endpoint: string;
  /** Service name reported in resource attributes. Defaults to window.location.hostname. */
  serviceName?: string;
  /** Service version reported in resource attributes. */
  serviceVersion?: string;
  /** Sampling rate 0-1. Default 1.0 (capture everything). */
  sampleRate?: number;
  /** Capture Core Web Vitals (LCP, FID, CLS, INP, TTFB). Default true. */
  captureWebVitals?: boolean;
  /** Capture JS errors and unhandled rejections as log records. Default true. */
  captureErrors?: boolean;
  /** Capture page navigation spans. Default true. */
  captureNavigations?: boolean;
  /** Capture resource timing spans (network waterfall). Default false (verbose). */
  captureResources?: boolean;
  /** Capture click/input interaction spans. Default false. */
  captureInteractions?: boolean;
  /** Inject traceparent header on fetch/XHR for backend correlation. Default true. */
  propagateTraceContext?: boolean;
  /** Number of spans/logs to batch before flushing. Default 10. */
  batchSize?: number;
  /** Flush interval in ms. Default 5000. */
  flushInterval?: number;
}

/** Resolved config with all defaults applied. */
export interface ResolvedConfig extends Required<QylConfig> {}

/** Global window extension for script tag config. */
declare global {
  interface Window {
    qyl?: Partial<QylConfig> | QylSdk;
  }
}

/** Public SDK handle returned from init(). */
export interface QylSdk {
  /** Flush all pending telemetry immediately. */
  flush(): Promise<void>;
  /** Shut down the SDK and flush remaining data. */
  shutdown(): Promise<void>;
  /** Manually record a span. */
  addSpan(span: OtlpSpan): void;
  /** Manually record a log. */
  addLog(log: OtlpLogRecord): void;
  /** The resolved configuration. */
  config: ResolvedConfig;
}
