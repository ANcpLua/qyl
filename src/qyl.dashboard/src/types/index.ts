/**
 * qyl API Types (aligned to OTel schema 1.39.0)
 * DO NOT edit api.ts directly - it's auto-generated from openapi.yaml
 *
 * Type Pipeline: TypeSpec -> OpenAPI -> openapi-typescript -> api.ts -> index.ts
 * Regenerate with: npm run generate:ts
 */

import type {components, operations, paths} from './api';

// =============================================================================
// Schemas (Canonical Names from God Schema)
// =============================================================================

// Primitives
export type TraceId = components['schemas']['Qyl.Common.TraceId'];
export type SpanId = components['schemas']['Qyl.Common.SpanId'];
export type SessionId = components['schemas']['Qyl.Common.SessionId'];
export type UnixNano = number; // int64 in schema
export type DurationNs = components['schemas']['Qyl.Common.DurationNs'];
export type DurationMs = components['schemas']['Qyl.Common.DurationMs'];
export type TokenCount = components['schemas']['Qyl.Common.Count'];
export type Temperature = number;
export type CostUsd = number;
export type Count = components['schemas']['Qyl.Common.Count'];
export type Ratio = components['schemas']['Qyl.Common.Ratio'];
export type Percentage = components['schemas']['Qyl.Common.Percentage'];

// Enums
export type SpanKind = components['schemas']['Qyl.OTel.Enums.SpanKind'];
export type SpanStatusCode = components['schemas']['Qyl.OTel.Enums.SpanStatusCode'];
export type SeverityNumber = components['schemas']['Qyl.OTel.Enums.SeverityNumber'];

// Models
export type Span = components['schemas']['Qyl.OTel.Traces.Span'];
export type SpanEvent = components['schemas']['Qyl.OTel.Traces.SpanEvent'];
export type SpanLink = components['schemas']['Qyl.OTel.Traces.SpanLink'];
export type SpanStatus = components['schemas']['Qyl.OTel.Traces.SpanStatus'];
export type Trace = components['schemas']['Qyl.OTel.Traces.Trace'];
export type Resource = components['schemas']['Qyl.OTel.Resource.Resource'];
export type Attribute = components['schemas']['Qyl.Common.Attribute'];
export type AttributeValue = components['schemas']['Qyl.Common.AttributeValue'];

// Session types
export type SessionEntity = components['schemas']['Qyl.Domains.Observe.Session.SessionEntity'];
export type SessionState = components['schemas']['Qyl.Domains.Observe.Session.SessionState'];
export type SessionClientInfo = components['schemas']['Qyl.Domains.Observe.Session.SessionClientInfo'];
export type SessionGeoInfo = components['schemas']['Qyl.Domains.Observe.Session.SessionGeoInfo'];
export type SessionGenAiUsage = components['schemas']['Qyl.Domains.Observe.Session.SessionGenAiUsage'];

// Error types
export type ProblemDetails = components['schemas']['Qyl.Common.Errors.ProblemDetails'];
export type ValidationError = components['schemas']['Qyl.Common.Errors.ValidationError'];
export type NotFoundError = components['schemas']['Qyl.Common.Errors.NotFoundError'];

// Health
export type HealthResponse = components['schemas']['HealthResponse'];
export type HealthStatus = components['schemas']['HealthStatus'];

// =============================================================================
// Legacy Aliases for Backward Compatibility
// =============================================================================

/** @deprecated Use Span instead */
export type SpanRecord = Span;

/** @deprecated Use SessionEntity instead */
export type SessionSummary = SessionEntity;

/** @deprecated Use SessionEntity instead */
export type Session = SessionEntity;

/** @deprecated Use Trace instead */
export type TraceNode = Trace;

/** @deprecated Use SpanStatusCode instead */
export type StatusCode = SpanStatusCode;

// =============================================================================
// Operations (from api.ts)
// =============================================================================
export type ApiOperations = operations;
export type ApiPaths = paths;

// Sessions
export type ListSessionsQuery = operations['SessionsApi_list']['parameters']['query'];
export type ListSessionsResponse =
    operations['SessionsApi_list']['responses']['200']['content']['application/json'];

export type GetSessionPath = operations['SessionsApi_get']['parameters']['path'];
export type GetSessionResponse =
    operations['SessionsApi_get']['responses']['200']['content']['application/json'];

export type GetSessionTracesPath = operations['SessionsApi_getTraces']['parameters']['path'];
export type GetSessionTracesResponse =
    operations['SessionsApi_getTraces']['responses']['200']['content']['application/json'];

// Traces
export type ListTracesQuery = operations['TracesApi_list']['parameters']['query'];
export type ListTracesResponse =
    operations['TracesApi_list']['responses']['200']['content']['application/json'];

export type GetTracePath = operations['TracesApi_get']['parameters']['path'];
export type GetTraceResponse =
    operations['TracesApi_get']['responses']['200']['content']['application/json'];

export type GetTraceSpansPath = operations['TracesApi_getSpans']['parameters']['path'];
export type GetTraceSpansResponse =
    operations['TracesApi_getSpans']['responses']['200']['content']['application/json'];

// Streaming
export type StreamTracesQuery = operations['StreamingApi_streamTraces']['parameters']['query'];
export type StreamTraceSpansPath = operations['StreamingApi_streamTraceSpans']['parameters']['path'];

// Health
export type HealthCheckResponse =
    operations['HealthApi_check']['responses']['200']['content']['application/json'];

// =============================================================================
// Utility Functions for Working with Span
// =============================================================================

/** Convert nanoseconds to milliseconds */
export function nsToMs(ns: number): number {
    return ns / 1_000_000;
}

/** Convert nanoseconds timestamp to ISO string */
export function nanoToIso(nanos: number): string {
    return new Date(nanos / 1_000_000).toISOString();
}

/** Parse JSON safely, returning empty object on failure */
export function parseJson<T>(json: string | undefined | null, fallback: T): T {
    if (!json) return fallback;
    try {
        return JSON.parse(json) as T;
    } catch {
        return fallback;
    }
}

/** Get attributes from Span as a record */
export function getAttributesRecord(span: Span): Record<string, unknown> {
    if (!span.attributes) return {};
    const result: Record<string, unknown> = {};
    for (const attr of span.attributes) {
        result[attr.key] = attr.value;
    }
    return result;
}

/** Get resource attributes from Span as a record */
export function getResourceRecord(span: Span): Record<string, unknown> {
    if (!span.resource?.attributes) return {};
    const result: Record<string, unknown> = {};
    for (const attr of span.resource.attributes) {
        result[attr.key] = attr.value;
    }
    return result;
}

/** Calculate duration in milliseconds from Span */
export function getSpanDurationMs(span: Span): number {
    return nsToMs(span.end_time_unix_nano - span.start_time_unix_nano);
}

/** Flatten trace to array of spans */
export function flattenTrace(trace: Trace): Span[] {
    return trace.spans;
}

/** Get primary service name from session (service.name not in SessionEntity schema yet) */
export function getPrimaryService(_session: SessionEntity): string {
    // TODO: Add service.name to SessionEntity schema
    return 'unknown';
}

// =============================================================================
// Status Code Helpers
// =============================================================================

/** StatusCode enum values (OTel uses integers: 0=UNSET, 1=OK, 2=ERROR) */
export const STATUS_UNSET: SpanStatusCode = 0;
export const STATUS_OK: SpanStatusCode = 1;
export const STATUS_ERROR: SpanStatusCode = 2;

/** Check if span has error status */
export function isErrorSpan(span: Span): boolean {
    return span.status.code === STATUS_ERROR;
}

/** Get status label from numeric code */
export function getStatusLabel(code: SpanStatusCode): string {
    switch (code) {
        case 0: return 'unset';
        case 1: return 'ok';
        case 2: return 'error';
        default: return 'unknown';
    }
}

// =============================================================================
// Log Types (not in OpenAPI schema - logs endpoint TBD)
// =============================================================================

/** Log level for UI display */
export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';

/** Log record for LogsPage */
export interface LogRecord {
    timestamp: string;
    observedTimestamp: string;
    traceId?: string;
    spanId?: string;
    severityNumber: number;
    severityText: LogLevel;
    body: string;
    attributes: Record<string, string | number | boolean | string[] | number[] | boolean[]>;
    serviceName: string;
    serviceVersion?: string;
}
