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

/** Exact nanosecond delta between two absolute wire timestamps. */
export function nsDelta(fromNs: string, toNs: string): number {
    return Number(BigInt(toNs) - BigInt(fromNs));
}

/** Ascending comparator for absolute wire timestamps, exact at nanosecond resolution. */
export function compareNs(a: string, b: string): number {
    const left = BigInt(a);
    const right = BigInt(b);
    return left < right ? -1 : left > right ? 1 : 0;
}

/** Absolute wire timestamp to epoch milliseconds, floored in BigInt so no precision is assumed. */
export function nsToEpochMs(ns: string): number {
    return Number(BigInt(ns) / 1_000_000n);
}

export function nanoToIso(nanos: string): string {
    return new Date(nsToEpochMs(nanos)).toISOString();
}

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
