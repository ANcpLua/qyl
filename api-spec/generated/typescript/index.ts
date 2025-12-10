/**
 * qyl API Types
 * 
 * This file re-exports generated types from the OpenAPI spec with convenient aliases.
 * DO NOT edit api.ts directly - it's auto-generated from openapi.yaml
 * 
 * Regenerate with: npm run generate:ts
 */

import type { components, operations, paths } from './api';

// =============================================================================
// Core Types - Re-exported with convenient names
// =============================================================================

/** OpenTelemetry Span with GenAI extensions */
export type Span = components['schemas']['Span'];

/** GenAI-specific data extracted from span attributes */
export type GenAISpanData = components['schemas']['GenAISpanData'];

/** Span event (logs attached to spans) */
export type SpanEvent = components['schemas']['SpanEvent'];

/** Span link (cross-trace references) */
export type SpanLink = components['schemas']['SpanLink'];

/** OpenTelemetry SpanKind */
export type SpanKind = components['schemas']['SpanKind'];

/** OpenTelemetry StatusCode */
export type SpanStatus = components['schemas']['SpanStatus'];

// =============================================================================
// Session Types
// =============================================================================

/** Aggregated session with GenAI statistics */
export type Session = components['schemas']['Session'];

/** GenAI statistics aggregated at session level */
export type SessionGenAIStats = components['schemas']['SessionGenAIStats'];

// =============================================================================
// Response Types
// =============================================================================

export type SessionListResponse = components['schemas']['SessionListResponse'];
export type SpanListResponse = components['schemas']['SpanListResponse'];
export type TraceResponse = components['schemas']['TraceResponse'];

// =============================================================================
// Realtime Types
// =============================================================================

export type TelemetryEvent = components['schemas']['TelemetryEvent'];
export type SpanBatch = components['schemas']['SpanBatch'];

// =============================================================================
// Auth Types
// =============================================================================

export type LoginRequest = components['schemas']['LoginRequest'];
export type LoginResponse = components['schemas']['LoginResponse'];
export type AuthCheckResponse = components['schemas']['AuthCheckResponse'];

// =============================================================================
// Health Types
// =============================================================================

export type HealthResponse = components['schemas']['HealthResponse'];

// =============================================================================
// API Operations - For type-safe fetch wrappers
// =============================================================================

export type ApiOperations = operations;
export type ApiPaths = paths;

// Operation-specific types for building API clients
export type GetSessionsParams = operations['getSessions']['parameters']['query'];
export type GetSessionsResponse = operations['getSessions']['responses']['200']['content']['application/json'];

export type GetSessionParams = operations['getSession']['parameters']['path'];
export type GetTraceParams = operations['getTrace']['parameters']['path'];

// =============================================================================
// Type Guards - Runtime validation
// =============================================================================

export function isSpanKind(value: string): value is SpanKind {
  return ['unspecified', 'internal', 'server', 'client', 'producer', 'consumer'].includes(value);
}

export function isSpanStatus(value: string): value is SpanStatus {
  return ['unset', 'ok', 'error'].includes(value);
}

export function isGenAISpan(span: Span): span is Span & { genai: GenAISpanData } {
  return span.genai !== null && span.genai !== undefined;
}

export function hasErrors(session: Session): boolean {
  return session.errorCount > 0;
}

// =============================================================================
// Utility Types
// =============================================================================

/** Span with guaranteed GenAI data */
export type GenAISpan = Span & { genai: GenAISpanData };

/** Filter all GenAI spans from a list */
export function filterGenAISpans(spans: Span[]): GenAISpan[] {
  return spans.filter(isGenAISpan);
}

/** Calculate total tokens for a span */
export function getTotalTokens(span: Span): number | null {
  if (!span.genai) return null;
  const { inputTokens, outputTokens } = span.genai;
  if (inputTokens == null && outputTokens == null) return null;
  return (inputTokens ?? 0) + (outputTokens ?? 0);
}

/** Format duration in human-readable form */
export function formatDuration(ms: number): string {
  if (ms < 1) return `${(ms * 1000).toFixed(0)}μs`;
  if (ms < 1000) return `${ms.toFixed(1)}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(2)}s`;
  return `${(ms / 60000).toFixed(1)}m`;
}

/** Get primary service from session (first in array) */
export function getPrimaryService(session: Session): string {
  return session.services[0] ?? 'unknown';
}
