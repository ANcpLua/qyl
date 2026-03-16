# GenAI Runtime Controls Specification

In-process LLM middleware pipeline. No proxy. No network hop. DelegatingChatClient chain in the standard .NET IChatClient pipeline.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Pipeline](#2-pipeline)
3. [Middleware](#3-middleware)
4. [Cost Tracking](#4-cost-tracking)
5. [Non-.NET Integration](#5-non-dotnet)
6. [Definition of Done](#6-definition-of-done)

---

## 1. Overview

GenAI controls live in `qyl.servicedefaults` as `DelegatingChatClient` middleware. They execute in-process, with no network hop, no proxy, and no critical-path dependency on qyl's availability.

Design principle: qyl is an observability platform — side channel. If qyl goes down, apps keep working. A proxy sits in the critical path. These are fundamentally different reliability contracts.

See `decisions/no-proxy.md` for the full rationale.

## 2. Pipeline

```csharp
builder.Services.AddChatClient(new OpenAIChatClient(...))
    .UseQylInstrumentation()   // OTel spans with GenAI semconv 1.40
    .UseQylSemanticCache()     // check DuckDB/memory before calling LLM
    .UseQylRateLimit()         // token bucket per model/tenant
    .UseQylPiiRedaction()      // scrub PII before send and before store
    .UseQylGuards()            // content policy enforcement
    .UseQylFallback();         // failover to backup model/provider
```

Each middleware is a `DelegatingChatClient`. Order matters: pipeline executes top-down on request, bottom-up on response.

Keys never leave the application process. Full access to application context (user, session, trace correlation).

The Roslyn source generators (`[GenAi]` attribute) can wire this pipeline automatically for attributed call sites.

## 3. Middleware

### 3.1 UseQylInstrumentation

Already implemented as `InstrumentedChatClient`.

- Wraps call in `Activity` span
- Records GenAI semconv 1.40 attributes:
  - `gen_ai.system` — provider name
  - `gen_ai.request.model` — model identifier
  - `gen_ai.request.temperature` — sampling temperature
  - `gen_ai.usage.input_tokens` — prompt token count
  - `gen_ai.usage.output_tokens` — completion token count
- Emits span via OTLP to collector
- Captures tool calls as child spans via `InstrumentedAIFunction`

### 3.2 UseQylSemanticCache

Check DuckDB or in-memory cache for semantically similar recent requests before calling the LLM.

- Embedding-based similarity matching
- Configurable similarity threshold
- Cache TTL per model/use-case
- Cache hit recorded as span event

### 3.3 UseQylRateLimit

Token bucket rate limiting per model, per tenant.

- Input token budget per time window
- Output token budget per time window
- Configurable burst allowance
- Rate limit hit recorded as span event with retry-after

### 3.4 UseQylPiiRedaction

Scrub PII from prompts before sending to LLM and from responses before storing in DuckDB.

- Configurable entity types (email, phone, SSN, etc.)
- Redaction recorded as span event with entity counts
- Original content never stored

### 3.5 UseQylGuards

Content policy enforcement.

- Input guards: block prompts matching policy rules
- Output guards: block responses matching policy rules
- Guard violations recorded as span events with policy ID
- Configurable action: block, warn, log-only

### 3.6 UseQylFallback

Provider failover chain.

- Primary → secondary → tertiary model
- Failover triggered by: timeout, rate limit, provider error
- Failover recorded as span event with reason
- Configurable timeout per provider

## 4. Cost Tracking

Collector-side feature. No proxy needed.

The collector already ingests `gen_ai.usage.input_tokens` and `gen_ai.usage.output_tokens` in GenAI spans. Cost computation:

```text
model pricing table (configurable) × token counts = per-call cost
```

Aggregations:

- Per-call cost
- Per-session cost
- Per-service cost
- Per-model cost over time
- Budget alerts when spend exceeds threshold

Pricing table stored in DuckDB. Updated via MCP management tools or REST API.

## 5. Non-.NET Integration

Non-.NET applications:

1. Emit GenAI semconv spans to qyl's OTLP endpoint using their language's OTel SDK
2. Use their ecosystem's middleware for runtime controls (rate limiting, caching, etc.)
3. qyl observes the results — same DuckDB storage, same dashboard, same MCP tools

qyl does not provide runtime middleware for Python, TypeScript, Go, etc. It provides the observability layer. Runtime controls are the app's responsibility. The proxy layer is someone else's product.

## 6. Definition of Done

- [ ] Each middleware is an independent DelegatingChatClient
- [ ] Pipeline order enforced at registration time
- [ ] All middleware actions recorded as span events
- [ ] Telemetry lands in DuckDB GenAI tables via normal OTLP
- [ ] Cost computed from token counts × pricing table
- [ ] Cost aggregations available via REST API and MCP tools
- [ ] Non-.NET apps can emit GenAI spans via standard OTel SDK
- [ ] No proxy, no network hop, no critical-path dependency on qyl
