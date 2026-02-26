# qyl.browser - Browser SDK

Browser surface of qyl. Lightweight OTLP SDK — captures web vitals, errors, interactions and sends to the kernel (
collector).

## Role in Architecture

One of three shells (browser, terminal, IDE) on the qyl kernel. The browser SDK instruments the customer's frontend —
their users' browsers send telemetry directly to the collector. The dashboard (qyl.dashboard) is a separate surface for
*viewing* data; this SDK *generates* data from end-user browsers.

## Identity

| Property | Value                   |
|----------|-------------------------|
| Package  | @qyl/browser            |
| Version  | 0.1.0                   |
| Format   | ESM + IIFE (script tag) |
| Build    | Vite 7 + TypeScript 5.9 |
| Target   | ES2022                  |
| Peer Dep | react >=18 (optional)   |

## Exports

| Path                  | Format | Purpose                     |
|-----------------------|--------|-----------------------------|
| `@qyl/browser`        | ESM    | `init()`, types             |
| `@qyl/browser/react`  | ESM    | `QylProvider` component     |
| `@qyl/browser/script` | IIFE   | Auto-init from `window.qyl` |

## Usage

```ts
// ESM
import { init } from '@qyl/browser';
const sdk = init({ endpoint: 'http://localhost:5100' });

// React
import { QylProvider } from '@qyl/browser/react';
<QylProvider config={{ endpoint: 'http://localhost:5100' }}><App /></QylProvider>


// Script tag
<script>window.qyl = { endpoint: 'http://localhost:5100' };</script>
<script src="/qyl.js"></script>
```

## Collectors

| Module            | Default | What it captures                           |
|-------------------|---------|--------------------------------------------|
| `web-vitals.ts`   | on      | LCP, CLS, INP, TTFB as spans               |
| `errors.ts`       | on      | JS errors + unhandled rejections as logs   |
| `navigation.ts`   | on      | Page navigations via Navigation Timing API |
| `resources.ts`    | off     | Network waterfall (CSS, JS, images, fonts) |
| `interactions.ts` | off     | Click events with element selectors        |
| `context.ts`      | on      | W3C traceparent injection on fetch + session ID |

## Files

| File                  | Purpose                                            |
|-----------------------|----------------------------------------------------|
| `src/core.ts`         | `init()` — config resolution, collector wiring     |
| `src/transport.ts`    | OTLP JSON transport, batching, sendBeacon fallback |
| `src/types.ts`        | All OTLP + config TypeScript interfaces            |
| `src/context.ts`      | Trace/span ID generation, session ID, fetch patching |
| `src/web-vitals.ts`   | Core Web Vitals → OTLP spans                       |
| `src/errors.ts`       | Error/rejection → OTLP logs                        |
| `src/navigation.ts`   | Navigation Timing → OTLP spans                     |
| `src/resources.ts`    | Resource Timing → OTLP spans                       |
| `src/interactions.ts` | Click events → OTLP spans                          |
| `src/react.ts`        | `QylProvider` React component                      |
| `src/script.ts`       | IIFE auto-init from `window.qyl`                   |
| `src/index.ts`        | ESM barrel export                                  |
| `vite.config.ts`      | ESM build (index + react entries)                  |
| `vite.iife.config.ts` | IIFE build (qyl.js script tag bundle)              |

## Session Correlation

Every OTLP payload includes a `session.id` resource attribute (32-char hex, generated once per `init()`).
This groups all telemetry from one browser tab without forcing a single mega-trace.

- Per-interaction trace model: each web vital, navigation, click, fetch = own trace
- `session.id` on the resource covers all spans and logs in every payload
- Error logs get their own `traceId` + `spanId` for correlation
- Server-side correlation: `patchFetch` injects `traceparent` → server spans share the fetch trace

```
Session: session.id=XXXX (resource attribute)
  |-- Trace 1: web-vital.LCP      (browser only)
  |-- Trace 2: navigation /home   (browser only)
  |-- Trace 3: fetch /api/data    traceparent → server span correlated
  \-- Trace 4: JS error           log with own traceId
```

## Transport

- Sends to `/v1/traces` (spans) and `/v1/logs` (log records)
- Standard OTLP HTTP JSON format with `resourceSpans`/`resourceLogs` envelopes
- `session.id` included in resource attributes on every payload
- Batches by `batchSize` (default 10) and `flushInterval` (default 5s)
- Uses `navigator.sendBeacon` on page visibility change for reliable unload
- Never throws — all transport errors silently dropped

## Config

| Option                  | Default             | Description                 |
|-------------------------|---------------------|-----------------------------|
| `endpoint`              | required            | Collector URL               |
| `serviceName`           | `location.hostname` | Resource attribute          |
| `serviceVersion`        | `''`                | Resource attribute          |
| `sampleRate`            | `1.0`               | 0-1 sampling rate           |
| `captureWebVitals`      | `true`              | Core Web Vitals             |
| `captureErrors`         | `true`              | JS errors as logs           |
| `captureNavigations`    | `true`              | Page navigations            |
| `captureResources`      | `false`             | Network waterfall           |
| `captureInteractions`   | `false`             | Click tracking              |
| `propagateTraceContext` | `true`              | Inject traceparent on fetch |
| `batchSize`             | `10`                | Spans/logs per flush        |
| `flushInterval`         | `5000`              | Flush interval (ms)         |

## Rules

- Never throw to the application — all errors swallowed in transport
- Skip OTLP requests to collector in resource observer (avoid feedback loops)
- Only inject traceparent on same-origin requests (not cross-origin, not to collector)
- web-vitals is the only runtime dependency
- React is optional peer dependency
