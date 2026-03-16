# Browser SDK Specification

Client-side telemetry collection for web applications. Web Vitals, user interactions, and navigation tracking.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Web Vitals](#2-web-vitals)
3. [Interactions](#3-interactions)
4. [Export](#4-export)
5. [Known Gaps](#5-known-gaps)
6. [Definition of Done](#6-definition-of-done)

---

## 1. Overview

`src/qyl.browser/` — TypeScript SDK for browser-side telemetry.

Key files:

- `core.ts` — SDK initialization and configuration
- `context.ts` — trace context management
- `errors.ts` — error capture and formatting

Emits spans to qyl's OTLP HTTP endpoint.

## 2. Web Vitals

Captures Core Web Vitals as OTel metrics:

- LCP (Largest Contentful Paint)
- FID (First Input Delay) / INP (Interaction to Next Paint)
- CLS (Cumulative Layout Shift)
- TTFB (Time to First Byte)
- FCP (First Contentful Paint)

## 3. Interactions

Captures user interactions as spans:

- Click events with target element metadata
- Navigation events (route changes)
- Form submissions
- Custom events via SDK API

## 4. Export

Exports telemetry via OTLP HTTP (protobuf) to `QYL_OTLP_PORT`.

Batching and retry built into the export pipeline.

## 5. Known Gaps

- Creates isolated traces per event — no parent correlation with backend request traces
- No automatic session correlation
- No replay/session recording capability
- No source map integration for error stack traces

## 6. Definition of Done

- [ ] Web Vitals captured and exported as OTel metrics
- [ ] User interactions captured as spans with element metadata
- [ ] OTLP HTTP export with batching and retry
- [ ] SDK initializable with single script tag or npm import
- [ ] Telemetry visible in qyl dashboard alongside backend traces
