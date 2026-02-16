/**
 * W3C Trace Context (traceparent) generation and propagation.
 * Generates trace/span IDs and injects traceparent headers on fetch/XHR.
 */

const HEX = '0123456789abcdef';

/** Generate a random hex string of the given byte length. */
function randomHex(bytes: number): string {
    const arr = new Uint8Array(bytes);
    crypto.getRandomValues(arr);
    let out = '';
    for (let i = 0; i < arr.length; i++) {
        out += HEX[arr[i] >> 4] + HEX[arr[i] & 0xf];
    }
    return out;
}

/** Generate a 32-char (16 byte) trace ID. */
export function generateTraceId(): string {
    return randomHex(16);
}

/** Generate a 16-char (8 byte) span ID. */
export function generateSpanId(): string {
    return randomHex(8);
}

/** Format a W3C traceparent header value. */
export function formatTraceparent(traceId: string, spanId: string, sampled: boolean): string {
    return `00-${traceId}-${spanId}-${sampled ? '01' : '00'}`;
}

/** Convert nanosecond timestamp string to high-res nano string. */
export function hrTimeToNano(): string {
    // performance.timeOrigin + performance.now() gives sub-ms precision
    const now = performance.timeOrigin + performance.now();
    // Convert ms to nanoseconds as a string (avoid BigInt for bundle size)
    const ms = Math.floor(now);
    const frac = now - ms;
    const nanos = Math.round(frac * 1_000_000);
    return `${ms}${String(nanos).padStart(6, '0')}`;
}

/** Timestamp in OTLP nanosecond format from Date.now(). */
export function dateNowNano(): string {
    const ms = Date.now();
    return `${ms}000000`;
}

let originalFetch: typeof fetch | null = null;

/**
 * Patch global fetch to inject traceparent headers on same-origin / allowed requests.
 * Only patches requests going to the same origin (to avoid leaking trace context to third parties).
 */
export function patchFetch(endpoint: string): void {
    if (originalFetch) return; // already patched
    originalFetch = window.fetch;
    const collectorOrigin = new URL(endpoint).origin;

    window.fetch = function patchedFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
        const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;

        // Don't inject on requests to the collector itself or cross-origin
        let shouldInject = false;
        try {
            const reqOrigin = new URL(url, location.origin).origin;
            shouldInject = reqOrigin === location.origin && reqOrigin !== collectorOrigin;
        } catch {
            // relative URL — same origin
            shouldInject = location.origin !== collectorOrigin;
        }

        if (shouldInject) {
            const traceId = generateTraceId();
            const spanId = generateSpanId();
            const headers = new Headers(init?.headers);
            if (!headers.has('traceparent')) {
                headers.set('traceparent', formatTraceparent(traceId, spanId, true));
            }
            init = {...init, headers};
        }

        return originalFetch!.call(window, input, init);
    };
}

/** Restore original fetch. */
export function unpatchFetch(): void {
    if (originalFetch) {
        window.fetch = originalFetch;
        originalFetch = null;
    }
}
