/**
 * qyl API Types (aligned to OTel schema 1.40.0)
 * DO NOT edit api.ts directly - it's auto-generated from openapi.yaml
 *
 * Type Pipeline: TypeSpec -> OpenAPI -> openapi-typescript -> api.ts -> index.ts
 * Regenerate with: npm run generate:ts
 */

import type {components} from './api';

// =============================================================================
// Schemas (Canonical Names from God Schema)
// =============================================================================

// Enums
export type SpanStatusCode = components['schemas']['Qyl.OTel.Enums.SpanStatusCode'];

// Models
export type Span = components['schemas']['Qyl.OTel.Traces.Span'];

// Session types
export type SessionEntity = components['schemas']['Qyl.Domains.Observe.Session.SessionEntity'];

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

/** Get attributes from Span as a record */
export function getAttributesRecord(span: Span): Record<string, unknown> {
    if (!span.attributes) return {};
    const result: Record<string, unknown> = {};
    for (const attr of span.attributes) {
        result[attr.key] = attr.value;
    }
    return result;
}

/** Get primary service name from session */
export function getPrimaryService(session: SessionEntity): string {
    const services = (session as SessionEntity & { services?: string[] }).services;
    return services?.[0] ?? 'unknown';
}

// =============================================================================
// Status Code Helpers
// =============================================================================

/** StatusCode enum values (OTel uses integers: 0=UNSET, 1=OK, 2=ERROR) */
export const STATUS_ERROR: SpanStatusCode = 2;

/** Get status label from numeric code */
export function getStatusLabel(code: SpanStatusCode): string {
    switch (code) {
        case 0:
            return 'unset';
        case 1:
            return 'ok';
        case 2:
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
