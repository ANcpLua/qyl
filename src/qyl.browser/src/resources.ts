/**
 * Resource timing span capture for network waterfall visualization.
 * Captures CSS, JS, images, fonts, XHR, and fetch resource loads.
 */

import type { OtlpSpan } from './types.js';
import { generateTraceId, generateSpanId } from './context.js';
import type { Transport } from './transport.js';

function msToNano(ms: number, origin: number): string {
  const totalMs = origin + ms;
  const whole = Math.floor(totalMs);
  const frac = totalMs - whole;
  const nanos = Math.round(frac * 1_000_000);
  return `${whole}${String(nanos).padStart(6, '0')}`;
}

function resourceToSpan(entry: PerformanceResourceTiming): OtlpSpan {
  const origin = performance.timeOrigin;
  let path: string;
  try {
    path = new URL(entry.name).pathname;
  } catch {
    path = entry.name;
  }

  return {
    traceId: generateTraceId(),
    spanId: generateSpanId(),
    name: `resource ${entry.initiatorType} ${path}`,
    kind: 3, // CLIENT
    startTimeUnixNano: msToNano(entry.startTime, origin),
    endTimeUnixNano: msToNano(entry.responseEnd, origin),
    attributes: [
      { key: 'http.url', value: { stringValue: entry.name } },
      { key: 'resource.initiator_type', value: { stringValue: entry.initiatorType } },
      { key: 'resource.transfer_size', value: { intValue: String(entry.transferSize) } },
      { key: 'resource.encoded_body_size', value: { intValue: String(entry.encodedBodySize) } },
      { key: 'resource.decoded_body_size', value: { intValue: String(entry.decodedBodySize) } },
      { key: 'resource.duration_ms', value: { doubleValue: entry.duration } },
      { key: 'browser.url', value: { stringValue: location.href } },
    ],
  };
}

let observer: PerformanceObserver | null = null;

export function startResourceCapture(transport: Transport): void {
  if (typeof PerformanceObserver === 'undefined') return;

  try {
    observer = new PerformanceObserver((list) => {
      for (const entry of list.getEntries() as PerformanceResourceTiming[]) {
        // Skip OTLP requests to the collector to avoid feedback loops
        if (entry.name.includes('/v1/traces') || entry.name.includes('/v1/logs')) continue;
        transport.addSpan(resourceToSpan(entry));
      }
    });
    observer.observe({ type: 'resource', buffered: true });
  } catch {
    // PerformanceObserver not available
  }
}

export function stopResourceCapture(): void {
  if (observer) {
    observer.disconnect();
    observer = null;
  }
}
