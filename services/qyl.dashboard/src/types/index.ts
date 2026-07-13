import {
    SpanStatusCodeValues,
    type AttributeValue,
    type SessionEntity,
    type Span,
    type SpanStatusCode,
} from '@ancplua/qyl-api-schema/types';

export type {
    CursorPageLogRecord,
    CursorPageSessionEntity,
    CursorPageSpan,
    CursorPageTrace,
    HealthReport,
    LogRecord as ContractLogRecord,
    LogStreamEvent,
    SessionEntity,
    Span,
    SpanStatusCode,
    Trace,
} from '@ancplua/qyl-api-schema/types';

/** Convert nanoseconds to milliseconds. */
export function nsToMs(ns: number): number {
    return ns / 1_000_000;
}

/** Convert a nanosecond Unix timestamp to ISO 8601. */
export function nanoToIso(nanos: number): string {
    return new Date(nanos / 1_000_000).toISOString();
}

/** Get span attributes as a key/value record for UI lookup. */
export function getAttributesRecord(span: Span): Record<string, unknown> {
    if (!span.attributes) return {};
    const result: Record<string, unknown> = {};
    for (const attr of span.attributes) result[attr.key] = attr.value;
    return result;
}

export function getPrimaryService(session: SessionEntity): string {
    return session.services[0] ?? 'unknown';
}

export const STATUS_ERROR: SpanStatusCode = SpanStatusCodeValues.error;

export function getStatusLabel(code: SpanStatusCode): string {
    switch (code) {
        case SpanStatusCodeValues.unset:
            return 'unset';
        case SpanStatusCodeValues.ok:
            return 'ok';
        case SpanStatusCodeValues.error:
            return 'error';
    }
}

/** Display level derived from the OTel numeric/text severity pair. */
export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';

/** Dashboard-only projection optimized for rendering and filtering log rows. */
export interface LogViewRecord {
    timestamp: string;
    observedTimestamp: string;
    traceId?: string;
    spanId?: string;
    severityNumber: number;
    severityText: LogLevel;
    body: string;
    attributes: Record<string, AttributeValue>;
    serviceName: string;
    serviceVersion?: string;
}
