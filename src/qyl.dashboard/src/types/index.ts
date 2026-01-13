/**
 * qyl API Types (aligned to OTel schema 1.38.0)
 * DO NOT edit api.ts directly - it's auto-generated from openapi.yaml
 *
 * Type Pipeline: TypeSpec -> OpenAPI -> openapi-typescript -> api.ts -> index.ts
 * Regenerate with: npm run generate:ts
 */

import type { components, operations, paths } from './api';

// =============================================================================
// Schemas (Canonical Names from God Schema)
// =============================================================================
export type ApiError = components['schemas']['Api.ApiError'];

export type GenAiSpanData = components['schemas']['Models.GenAiSpanData'];
export type SessionSummary = components['schemas']['Models.SessionSummary'];
export type SpanRecord = components['schemas']['Models.SpanRecord'];
export type TraceNode = components['schemas']['Models.TraceNode'];

export type TraceId = components['schemas']['Primitives.TraceId'];
export type SpanId = components['schemas']['Primitives.SpanId'];
export type SessionId = components['schemas']['Primitives.SessionId'];
export type UnixNano = components['schemas']['Primitives.UnixNano'];
export type DurationNs = components['schemas']['Primitives.DurationNs'];
export type TokenCount = components['schemas']['Primitives.TokenCount'];
export type Temperature = components['schemas']['Primitives.Temperature'];
export type CostUsd = components['schemas']['Primitives.CostUsd'];
export type Count = components['schemas']['Primitives.Count'];

export type SpanKind = components['schemas']['Enums.SpanKind'];
export type StatusCode = components['schemas']['Enums.StatusCode'];
export type SeverityNumber = components['schemas']['Enums.SeverityNumber'];
export type GenAiOperationName = components['schemas']['Enums.GenAiOperationName'];
export type GenAiFinishReason = components['schemas']['Enums.GenAiFinishReason'];

// =============================================================================
// Operations (from api.ts)
// =============================================================================
export type ApiOperations = operations;
export type ApiPaths = paths;

export type LiveStreamQuery = operations['Live_stream']['parameters']['query'];
export type LiveStreamResponse =
  operations['Live_stream']['responses']['200']['content']['text/event-stream'];

export type ListSessionsQuery = operations['Sessions_list']['parameters']['query'];
export type ListSessionsResponse =
  operations['Sessions_list']['responses']['200']['content']['application/json'];

export type GetSessionPath = operations['Sessions_get']['parameters']['path'];
export type GetSessionResponse =
  operations['Sessions_get']['responses']['200']['content']['application/json'];

export type GetSessionSpansPath = operations['Sessions_getSpans']['parameters']['path'];
export type GetSessionSpansQuery = operations['Sessions_getSpans']['parameters']['query'];
export type GetSessionSpansResponse =
  operations['Sessions_getSpans']['responses']['200']['content']['application/json'];

export type ListSpansQuery = operations['Spans_list']['parameters']['query'];
export type ListSpansResponse =
  operations['Spans_list']['responses']['200']['content']['application/json'];

export type GetSpanPath = operations['Spans_get']['parameters']['path'];
export type GetSpanResponse =
  operations['Spans_get']['responses']['200']['content']['application/json'];

export type GetTracePath = operations['Traces_get']['parameters']['path'];
export type GetTraceResponse =
  operations['Traces_get']['responses']['200']['content']['application/json'];

export type GetTraceSpansPath = operations['Traces_getSpans']['parameters']['path'];
export type GetTraceSpansResponse =
  operations['Traces_getSpans']['responses']['200']['content']['application/json'];

export type HealthResponse =
  operations['Health_check']['responses']['200']['content']['application/json'];

// =============================================================================
// Type Aliases for Backward Compatibility
// =============================================================================

/** Session is alias for SessionSummary */
export type Session = SessionSummary;

// =============================================================================
// Utility Functions for Working with SpanRecord
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

/** Get parsed attributes from SpanRecord */
export function getAttributes(span: SpanRecord): Record<string, unknown> {
  return parseJson(span.attributesJson, {});
}

/** Get parsed resource from SpanRecord */
export function getResource(span: SpanRecord): Record<string, unknown> | undefined {
  return span.resourceJson ? parseJson(span.resourceJson, {}) : undefined;
}

/** Calculate total tokens from SpanRecord */
export function getTotalTokens(span: SpanRecord): number | undefined {
  const input = span.genAiInputTokens ?? 0;
  const output = span.genAiOutputTokens ?? 0;
  return input + output || undefined;
}

/** Flatten trace tree to array of spans */
export function flattenTraceTree(node: TraceNode): SpanRecord[] {
  const spans: SpanRecord[] = [node.span];
  for (const child of node.children) {
    spans.push(...flattenTraceTree(child));
  }
  return spans;
}

/** Get primary service name from session */
export function getPrimaryService(session: SessionSummary): string {
  return session.serviceName ?? session.genAiSystem ?? 'unknown';
}

// =============================================================================
// Status Code Helpers
// =============================================================================

/** StatusCode enum values */
export const STATUS_UNSET = 0 as StatusCode;
export const STATUS_OK = 1 as StatusCode;
export const STATUS_ERROR = 2 as StatusCode;

/** Check if span has error status */
export function isErrorSpan(span: SpanRecord): boolean {
  return span.statusCode === STATUS_ERROR;
}

/** Get status label */
export function getStatusLabel(code: StatusCode): string {
  switch (code) {
    case STATUS_UNSET:
      return 'unset';
    case STATUS_OK:
      return 'ok';
    case STATUS_ERROR:
      return 'error';
    default:
      return 'unknown';
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
