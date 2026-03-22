# Decision: No Proxy Gateway

## Status

Accepted.

## Context

Should qyl include an LLM proxy gateway that intercepts API calls to capture telemetry?

## Decision

No. qyl captures telemetry via OTel instrumentation (side channel), not interception (critical path).

This ADR is mechanically true only if the codebase enforces all of these invariants:

1. `qyl.collector` never terminates provider-compatible chat/completions APIs and never forwards LLM requests.
2. The server may depend on GenAI abstraction types (`Microsoft.Extensions.AI`) but not provider SDKs or reverse-proxy infrastructure.
3. GenAI observation happens in the SDK/instrumentation layer, not in the server request path.
4. Any helper that wraps an `IChatClient` emits telemetry only; it does not add retry, fallback, routing, caching, guardrails, or provider failover behavior.

## Rationale

A proxy sits in the critical path of every LLM call. If it goes down, all LLM calls fail. qyl is an observability platform — if qyl goes down, apps keep working; they just lose telemetry. These are fundamentally different reliability contracts.

Mixing proxy responsibilities into a process that also runs DuckDB, a dashboard, and an MCP server creates resource contention and operational risk.

Maintaining proxy compatibility with 100+ LLM provider APIs (OpenAI, Anthropic, Google, Cohere, Mistral, and counting) is a full team's ongoing work. Every provider API change becomes a qyl release.

## Canonical GenAI Observation Hooks

The current ADR text is too narrow. `InstrumentedChatClient` is not the only non-proxy path.

The canonical hooks should be:

1. Builder-based instrumentation for `ChatClientBuilder`
   - File: `src/qyl.instrumentation/Instrumentation/GenAi/ChatClientExtensions.cs`
   - Mechanism: `UseQylInstrumentation()` -> `builder.UseOpenTelemetry(...)`
   - Status: primary path for M.E.AI-native apps
2. Generated interceptor path for attributed methods
   - Files: `src/qyl.instrumentation.generators/Emitters/GenAiInterceptorEmitter.cs`,
     `src/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs`
   - Mechanism: `[GenAi]` interceptors call `GenAiInstrumentation.Execute*()`
   - Status: primary path for compile-time instrumentation of app code
3. Raw-client wrapper path
   - File: `src/qyl.instrumentation/Instrumentation/GenAi/InstrumentedChatClient.cs`
   - Mechanism: wrap an existing `IChatClient`
   - Status: supported escape hatch when the client already exists and no builder pipeline is available
4. Tool-call wrapper path
   - Files: `src/qyl.instrumentation/Instrumentation/GenAi/InstrumentedAIFunction.cs`,
     `src/qyl.instrumentation/Instrumentation/GenAi/ChatClientExtensions.cs`
   - Mechanism: `AddInstrumentedTools()` / `InstrumentedAIFunction`
   - Status: canonical hook for `execute_tool` spans

Opinionated call: builder-based instrumentation and generated interceptors should be first-class. `InstrumentedChatClient` should remain supported, but not co-equal as a second semconv implementation. Two independent GenAI span engines will drift. The wrapper should either delegate to the same underlying OTel hook or be documented as the narrow "raw `IChatClient` only" bridge.

## Mechanical Implementation Plan

### Impacted files

**Architecture and spec sync**

- `specs/decisions/no-proxy.md`
- `specs/00-architecture.md`
- `specs/instrumentation.md`
- `SUMMARY` surface for the repo's architecture/spec index

**Instrumentation runtime**

- `src/qyl.instrumentation/Instrumentation/GenAi/ChatClientExtensions.cs`
- `src/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs`
- `src/qyl.instrumentation/Instrumentation/GenAi/InstrumentedChatClient.cs`
- `src/qyl.instrumentation/Instrumentation/GenAi/InstrumentedAIFunction.cs`
- `src/qyl.instrumentation/Instrumentation/QylServiceDefaultsExtensions.cs`

**Generator path**

- `src/qyl.instrumentation.generators/Emitters/GenAiInterceptorEmitter.cs`
- `src/qyl.instrumentation.generators/Analyzers/GenAiCallSiteAnalyzer.cs`

**Server guardrails**

- `src/qyl.collector/qyl.collector.csproj`
- `tests/qyl.collector.tests/ArchitectureTests.cs`
- collector API/host tests that enumerate registered endpoints and service registrations

### Deletions

- Delete any provider-relay endpoint, adapter, or forwarding service if one exists or reappears.
- Delete any collector config for upstream provider base URLs, relay auth tokens, or provider pass-through headers.
- Delete duplicate custom GenAI span logic if `InstrumentedChatClient` can be reduced to the same OTel hook used by the builder path.

### Implementations

- Make builder-based `UseQylInstrumentation()` the documented default for `IChatClient` pipelines.
- Keep `[GenAi]` -> `GenAiInstrumentation.Execute*()` as the canonical compile-time hook for direct SDK/app methods.
- Keep `InstrumentedAIFunction` as the canonical tool-call span hook.
- Narrow `InstrumentedChatClient` to a bridge for already-constructed clients; do not let it become a middleware stack for non-observability features.
- Add explicit collector architecture tests banning:
  - provider SDK namespaces/packages (`OpenAI`, `Azure.AI.OpenAI`, `Anthropic`, `Google_GenerativeAI`, etc.)
  - reverse proxy packages/infrastructure (`Yarp`, relay services, provider-compatible controllers)
  - endpoint patterns that mimic provider APIs (`/v1/chat/completions`, `/responses`, `/messages`, embeddings relay routes)

### Spec changes

- `specs/00-architecture.md`: tighten section 3.1 from "no provider SDKs" to "no relay behavior, no provider-shaped endpoints, no outbound provider forwarding from the server."
- `specs/instrumentation.md`: make the canonical hook matrix explicit and stop implying `InstrumentedChatClient` is the only or preferred path.
- `SUMMARY`: add a one-line invariant that qyl observes via OTLP/OpenTelemetry instrumentation and never proxies provider traffic.

## Migration Sequence

1. Fix the docs first.
   - Align this ADR, architecture, instrumentation, and summary text around the same invariant and the same hook taxonomy.
2. Collapse the GenAI observation surface.
   - Choose one canonical runtime path for chat-client instrumentation.
   - Prefer builder-based OTel instrumentation as default.
   - Demote `InstrumentedChatClient` to bridge-only or re-implement it on top of the same hook.
3. Add hard server guardrails.
   - Ban provider SDK and proxy dependencies in `qyl.collector`.
   - Add tests that fail if provider-style relay endpoints are added.
4. Remove drift-enabling code.
   - Delete duplicate wrappers, relay-oriented options, and any provider-pass-through plumbing.
5. Lock the invariant with tests.
   - Architecture tests, endpoint-shape tests, and instrumentation tests become merge blockers.

## Validation / Tests

- `tests/qyl.collector.tests/ArchitectureTests.cs`
  - rename the summary/invariant away from vague "zero LLM dependencies"
  - assert: collector may use `Microsoft.Extensions.AI` abstractions only
  - assert: collector must not depend on provider SDKs or reverse-proxy packages
- Add collector API tests that enumerate mapped routes and fail on provider-shaped relay endpoints.
- Add instrumentation tests that prove:
  - `ChatClientBuilder.UseQylInstrumentation()` emits canonical GenAI spans
  - `[GenAi]` interception emits the same canonical attributes for direct method instrumentation
  - `InstrumentedChatClient` remains observational only and does not mutate routing/provider selection behavior
  - `AddInstrumentedTools()` emits `execute_tool` spans
- Add spec drift tests if the repo already has doc assertions; otherwise add a minimal architecture assertion suite rather than hand-wavy prose.

## Major Risks

- Keeping both `OpenTelemetryChatClient`-based and custom `InstrumentedChatClient` semconv implementations as equal "first-class" paths will drift in attributes, metrics, and future semconv updates.
- "No provider SDKs" alone is insufficient. A provider relay can re-enter via HTTP endpoints and generic `HttpClient` forwarding without any SDK package.
- Collector optional LLM abstractions for triage/autofix create pressure to smuggle in provider behavior. Keep those code paths explicitly non-request-relay and non-user-traffic-serving.
- If route-shape tests are omitted, someone will eventually add an innocuous-looking compatibility endpoint and recreate the proxy product by accident.

## Rejected Alternative

GenAI instrumentation is not "implemented as `InstrumentedChatClient`" full stop. The correct non-proxy alternative is an instrumentation stack:

- builder-based OpenTelemetry chat-client instrumentation
- compile-time `[GenAi]` interception
- tool-call instrumentation
- raw-client wrapping only where builder composition is unavailable

Middleware beyond instrumentation (caching, rate limiting, PII, guards, fallback) remains deliberately excluded. See `specs/00-architecture.md` section 1.1.
