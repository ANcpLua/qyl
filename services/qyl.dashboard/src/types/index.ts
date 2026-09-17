import {
    SpanStatusCodeValues,
    type AttributeValue,
    type Span,
    type SpanStatusCode,
} from '@ancplua/qyl-api-schema/types';

export type {
    SessionEntity,
    Span,
} from '@ancplua/qyl-api-schema/types';

// Absolute nanosecond timestamps cross the wire as decimal strings. A 2026 value is ~1.79e18,
// past Number.MAX_SAFE_INTEGER, so parsing one with Number() rounds it to the nearest 256 ns and
// two spans 100 ns apart collapse onto each other. Parse to BigInt and subtract there; only
// narrow to Number once the value is a small relative offset or an already-bounded duration.

/** Milliseconds from a *duration* in nanoseconds. Durations stay exact in Number until ~104 days. */
export function nsToMs(ns: number): number {
    return ns / 1_000_000;
}

export {
    compareUnixNanos as compareNs,
    unixNanosDelta as nsDelta,
    unixNanosToEpochMs as nsToEpochMs,
    unixNanosToIso as nanoToIso,
} from '@ancplua/qyl-api-schema/runtime';

export function getAttributesRecord(span: Span): Record<string, AttributeValue> {
    if (!span.attributes) return {};
    const result: Record<string, AttributeValue> = {};
    for (const attr of span.attributes) result[attr.key] = attr.value;
    return result;
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
