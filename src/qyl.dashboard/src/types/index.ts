/**
 * qyl API Types
 *
 * This file re-exports generated types from the OpenAPI spec with convenient aliases.
 * DO NOT edit api.ts directly - it's auto-generated from openapi.yaml
 *
 * Type Pipeline: TypeSpec → OpenAPI → openapi-typescript → api.ts → index.ts
 * Regenerate with: npm run generate:ts
 */

import type {components, operations, paths} from './api';

// =============================================================================
// Core Types - Re-exported with convenient names
// =============================================================================

/** OpenTelemetry Span with GenAI extensions */
export type Span = components['schemas']['Span'];

/** GenAI-specific data extracted from span attributes (OTel semconv 1.38) */
export type GenAiSpanData = components['schemas']['GenAiSpanData'];

/** Span event (logs attached to spans) */
export type SpanEvent = components['schemas']['SpanEvent'];

/** Span link (cross-trace references) */
export type SpanLink = components['schemas']['SpanLink'];

/** OpenTelemetry SpanKind */
export type SpanKind = components['schemas']['SpanKind'];

/** OpenTelemetry StatusCode */
export type SpanStatus = components['schemas']['SpanStatus'];

/** Key-value attributes map */
export type Attributes = components['schemas']['Attributes'];

// =============================================================================
// Session Types
// =============================================================================

/** Aggregated session with GenAI statistics */
export type Session = components['schemas']['Session'];

/** GenAI statistics aggregated at session level */
export type SessionGenAiStats = components['schemas']['SessionGenAiStats'];

// =============================================================================
// Response Types
// =============================================================================

export type SessionListResponse = components['schemas']['SessionListResponse'];
export type SpanListResponse = components['schemas']['SpanListResponse'];
export type TraceResponse = components['schemas']['TraceResponse'];
export type SpanBatch = components['schemas']['SpanBatch'];

// =============================================================================
// Console Log Types
// =============================================================================

export type ConsoleLogEntry = components['schemas']['ConsoleLogEntry'];
export type ConsoleLevel = components['schemas']['ConsoleLevel'];

// =============================================================================
// SSE Types
// =============================================================================

export type SseConnectedEvent = components['schemas']['SseConnectedEvent'];

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
export type ListSessionsParams = operations['Api_listSessions']['parameters']['query'];
export type ListSessionsResponse = operations['Api_listSessions']['responses']['200']['content']['application/json'];

export type GetSessionParams = operations['Api_getSession']['parameters']['path'];
export type GetTraceParams = operations['Api_getTrace']['parameters']['path'];

// =============================================================================
// Type Guards - Runtime validation
// =============================================================================

export function isSpanKind(value: string): value is SpanKind {
    return ['internal', 'server', 'client', 'producer', 'consumer'].includes(value);
}

export function isSpanStatus(value: string): value is SpanStatus {
    return ['unset', 'ok', 'error'].includes(value);
}

export function isGenAiSpan(span: Span): span is Span & { genai: GenAiSpanData } {
    return span.genai !== null && span.genai !== undefined;
}

export function hasErrors(session: Session): boolean {
    return session.errorCount > 0;
}

// =============================================================================
// Utility Types
// =============================================================================

/** Span with guaranteed GenAI data */
export type GenAiSpan = Span & { genai: GenAiSpanData };

/** Filter all GenAI spans from a list */
export function filterGenAiSpans(spans: Span[]): GenAiSpan[] {
    return spans.filter(isGenAiSpan);
}

/** Calculate total tokens for a span */
export function getTotalTokens(span: Span): number | null {
    if (!span.genai) return null;
    const {inputTokens, outputTokens} = span.genai;
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

// =============================================================================
// Extended Types - For UI features not yet in API
// These types extend the API for mock/preview features.
// =============================================================================

/** Extended GenAI message (for chat display, not in API yet) */
export interface GenAiMessage {
    role: string;
    content?: string;
    toolCalls?: GenAiToolCall[];
}

/** Extended GenAI tool call (for chat display, not in API yet) */
export interface GenAiToolCall {
    id: string;
    type: string;
    function: {
        name: string;
        arguments: string;
    };
}

/** Extended GenAI span data with message content (for GenAIPage mock data) */
export interface GenAiSpanDataExtended extends GenAiSpanData {
    finishReasons?: string[];
    inputMessages?: GenAiMessage[];
    outputMessages?: GenAiMessage[];
    toolCalls?: GenAiToolCall[];
}

/** Log level for OTel logs (differs from ConsoleLevel) */
export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';

/** OTel Log Record (for LogsPage - endpoint not yet implemented) */
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

// =============================================================================
// Legacy Type Aliases - For backwards compatibility during migration
// TODO: Remove these after updating all imports
// =============================================================================

/** @deprecated Use GenAiSpanData instead */
export type GenAISpanData = GenAiSpanData;

/** @deprecated Use SessionGenAiStats instead */
export type SessionGenAIStats = SessionGenAiStats;

/** @deprecated Use GenAiSpan instead */
export type GenAISpan = GenAiSpan;
