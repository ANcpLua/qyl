/**
 * Page navigation span capture via Performance Observer (Navigation Timing API).
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

function navigationToSpan(entry: PerformanceNavigationTiming): OtlpSpan {
  const origin = performance.timeOrigin;
  return {
    traceId: generateTraceId(),
    spanId: generateSpanId(),
    name: `navigation ${new URL(entry.name).pathname}`,
    kind: 3, // CLIENT
    startTimeUnixNano: msToNano(entry.startTime, origin),
    endTimeUnixNano: msToNano(entry.loadEventEnd || entry.responseEnd, origin),
    attributes: [
      { key: 'http.url', value: { stringValue: entry.name } },
      { key: 'navigation.type', value: { stringValue: entry.type } },
      { key: 'navigation.redirect_count', value: { intValue: String(entry.redirectCount) } },
      { key: 'navigation.dom_interactive_ms', value: { doubleValue: entry.domInteractive } },
      { key: 'navigation.dom_complete_ms', value: { doubleValue: entry.domComplete } },
      { key: 'navigation.load_event_end_ms', value: { doubleValue: entry.loadEventEnd } },
      { key: 'navigation.dom_content_loaded_ms', value: { doubleValue: entry.domContentLoadedEventEnd } },
      { key: 'navigation.transfer_size', value: { intValue: String(entry.transferSize) } },
      { key: 'navigation.decoded_body_size', value: { intValue: String(entry.decodedBodySize) } },
      { key: 'browser.url', value: { stringValue: location.href } },
    ],
  };
}

let observer: PerformanceObserver | null = null;

export function startNavigationCapture(transport: Transport): void {
  // Capture already-completed navigation entries
  const existing = performance.getEntriesByType('navigation') as PerformanceNavigationTiming[];
  for (const entry of existing) {
    transport.addSpan(navigationToSpan(entry));
  }

  // Observe future navigations (SPA soft navigations if supported)
  if (typeof PerformanceObserver !== 'undefined') {
    try {
      observer = new PerformanceObserver((list) => {
        for (const entry of list.getEntries() as PerformanceNavigationTiming[]) {
          transport.addSpan(navigationToSpan(entry));
        }
      });
      observer.observe({ type: 'navigation', buffered: true });
    } catch {
      // PerformanceObserver not available or type not supported
    }
  }
}

export function stopNavigationCapture(): void {
  if (observer) {
    observer.disconnect();
    observer = null;
  }
}
