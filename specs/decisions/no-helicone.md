# Decision: No Helicone Sidecar

## Status

Accepted.

## Context

Should qyl add Helicone OSS, OpenLLMetry compatibility, or any proxy-shaped sidecar to capture LLM telemetry?

## Decision

No.

qyl captures GenAI telemetry through OpenTelemetry instrumentation and OTLP ingestion. It does not proxy provider traffic, terminate provider-compatible APIs, or translate Helicone/OpenLLMetry schemas into first-class qyl data.

This ADR is mechanically true only if the codebase enforces all of these invariants:

1. Deprecated OTel normalization is allowed only for published OTel semconv migrations or qyl-owned legacy emitters.
2. Helicone/OpenLLMetry compatibility is forbidden. `llm.*`, `helicone.*`, Helicone headers, and proxy lifecycle concepts never become promoted columns, cost inputs, or product semantics.
3. The collector never grows sidecar/proxy behavior: no provider-shaped relay endpoints, no upstream forwarding, no sidecar config surface.
4. Canonical GenAI product behavior flows only from `gen_ai.*` semconv fields that qyl has chosen as canonical.

## Exact Boundary

### Allowed: deprecated OTel normalization

Keep normalization in one place only:

- `src/qyl.collector/Ingestion/OtlpAttributes.cs`
  - `SchemaNormalizer.DeprecatedMappings`

Allowed inputs:

- deprecated OTel GenAI names that map cleanly to current semconv, for example:
  - `gen_ai.system` -> `gen_ai.provider.name`
  - `gen_ai.usage.prompt_tokens` -> `gen_ai.usage.input_tokens`
  - `gen_ai.usage.completion_tokens` -> `gen_ai.usage.output_tokens`
- qyl-owned legacy prefixes already documented in the repo, for example `agents.*`

Notably, this is schema migration, not compatibility product work. The source key must already be an OTel semconv key or a qyl-owned historical key.

### Forbidden: Helicone/OpenLLMetry compatibility

Do not add or keep:

- `llm.*` -> `gen_ai.*` translation
- `helicone.*` -> qyl field translation
- Helicone request/response header handling such as `Helicone-Auth`, `Helicone-Target-Url`, or `X-Helicone-*`
- provider-compatible relay routes such as `/v1/chat/completions`, `/v1/responses`, `/v1/messages`, `/v1/embeddings`
- sidecar config or behavior in `qyl.collector`

Current drift to remove:

- `src/qyl.collector/Ingestion/OtlpConverter.cs`
  - stop hard-coding deprecated GenAI fallbacks outside `SchemaNormalizer`
  - do not add any `llm.*` fallback path here

## Sync Required With Neighbor Specs

### `specs/decisions/no-proxy.md`

Extend the no-proxy invariant from "no relay" to "no relay and no Helicone/OpenLLMetry compatibility layer." A compatibility endpoint that mimics Helicone is still a proxy product.

### `specs/collector.md`

Tighten section 2.3:

- canonical promoted GenAI fields come from current semconv `gen_ai.*`
- deprecated OTel names may be normalized before promotion
- non-semconv `llm.*` and `helicone.*` stay raw only and have no product meaning

Tighten section 2.2:

- `OtlpConverter` must consume already-normalized attributes
- normalization lives in `SchemaNormalizer`, not in ad hoc fallback code spread across ingestion

### `specs/cost.md`

Cost uses only canonical fields:

- `gen_ai_provider_name`
- `gen_ai_request_model`
- `gen_ai_input_tokens`
- `gen_ai_output_tokens`

No cost computation from `llm.model_name`, `llm.usage.prompt_tokens`, `llm.usage.completion_tokens`, or Helicone-specific metadata. If the sender does not emit canonical semconv, qyl stores the raw attributes but computes no cost.

### `specs/telemetry-data-model.md`

Clarify the model:

- promoted product columns are canonical semconv only
- deprecated OTel aliases may normalize into those canonical columns at ingest
- non-semconv `llm.*` and `helicone.*` stay in `attributes_json` / `resource_json`
- raw storage is acceptable; promotion is not

## Ingest and Promotion Policy

Opinionated rule:

- Preserve unknown `llm.*` attributes in overflow JSON for forensics.
- Do not normalize them.
- Do not create `llm_*` DuckDB promoted columns.
- Do not read them in cost, analytics, dashboard, issue grouping, or MCP query paths.

That keeps ingestion lossless without turning vendor drift into qyl semantics.

## Mechanical Implementation Plan

### Impacted files

**Implementations**

- `src/qyl.collector/Ingestion/OtlpAttributes.cs`
- `src/qyl.collector/Ingestion/OtlpConverter.cs`
- `src/qyl.collector/Cost/ModelPricingService.cs`
- `src/qyl.collector/Storage/DuckDbReaderExtensions.cs`
- `src/qyl.collector/Storage/promoted-columns.g.sql`
- `src/qyl.collector/Storage/DuckDbSchema.g.cs`

**Regression tests**

- `tests/qyl.collector.tests/Ingestion/OtlpConverterCapabilityTests.cs`
- new targeted ingestion test file for normalization boundaries
- `tests/qyl.collector.tests/Cost/CostComputationTests.cs`
- collector architecture or route-shape tests that ban sidecar/proxy behavior

**Spec changes**

- `specs/decisions/no-helicone.md`
- `specs/decisions/no-proxy.md`
- `specs/collector.md`
- `specs/cost.md`
- `specs/telemetry-data-model.md`

### Deletions

- Delete duplicated deprecated-GenAI fallback logic from `OtlpConverter.ExtractGenAiAttributes()`.
- Delete any future `llm.*` or Helicone mapping proposal at ingest time instead of adding "temporary" aliases.
- Delete any proxy/sidecar route, option, header parser, or forwarding service if it appears.

### Implementations

- Route all deprecated semconv canonicalization through `SchemaNormalizer`.
- Make `OtlpConverter` read canonical keys after normalization, not by probing old aliases inline.
- Keep cost enrichment strict: no canonical model/provider/tokens, no cost.
- Add explicit tests that `llm.*` remains raw overflow only.

### Patch sketch

1. In `OtlpAttributes.cs`, keep only OTel/qyl-owned mappings in `SchemaNormalizer`.
2. In `OtlpConverter.cs`, normalize attribute names first, then extract only canonical `gen_ai.*` keys.
3. In `ModelPricingService.cs`, leave computation strict and add tests proving raw `llm.*` spans do not get cost.
4. In schema/tests, assert no `llm_*` or `helicone_*` promoted columns and no provider-shaped relay routes.

## Regression Tests

Add tests that fail on these cases:

1. Deprecated OTel semconv still works.
   - `gen_ai.system` promotes into `gen_ai_provider_name`
   - `gen_ai.usage.prompt_tokens` promotes into `gen_ai_input_tokens`
2. OpenLLMetry does not become canonical.
   - `llm.model_name` does not populate `gen_ai_request_model`
   - `llm.usage.prompt_tokens` does not populate `gen_ai_input_tokens`
   - `llm.usage.completion_tokens` does not populate `gen_ai_output_tokens`
   - raw keys remain in `attributes_json`
3. Cost stays strict.
   - spans with only `llm.*` data compute no `gen_ai_cost_usd`
4. Proxy behavior stays banned.
   - no collector route map contains provider/Helicone relay endpoints
   - no collector package/reference introduces reverse-proxy infrastructure for this feature class

## Migration Sequence

1. Fix the specs.
   - Align this ADR, `no-proxy`, `collector`, `cost`, and `telemetry-data-model` around the same boundary.
2. Collapse ingestion logic.
   - Move all deprecated-key handling to `SchemaNormalizer`.
   - Remove duplicate fallback branches from `OtlpConverter`.
3. Lock promotion policy.
   - Canonical `gen_ai.*` only for promoted fields and cost inputs.
   - `llm.*` raw overflow only.
4. Add regression tests.
   - Ingestion normalization
   - strict cost behavior
   - no proxy/sidecar route shape
5. Validate against a mixed fixture set.
   - canonical semconv fixture
   - deprecated OTel fixture
   - OpenLLMetry/Helicone fixture that must remain non-canonical

## Validation

- Unit tests for `SchemaNormalizer` and `OtlpConverter`
- Unit tests for cost enrichment strictness
- Architecture/route tests banning sidecar and compatibility endpoints
- Schema assertion that promoted columns remain semconv-derived and do not grow `llm_*`

## Major Risks

- The repo already mixes "deprecated OTel normalization" with "legacy alias probing" in multiple places. If that stays split, Helicone compatibility will creep in by accident.
- The wider repo still documents `gen_ai.system` as if it were canonical. If specs stay inconsistent, code cleanup will stall.
- Raw-overflow storage can be mistaken for support. The ADR must say plainly: preserved is not supported.
- Route-shape tests matter. Without them, someone can reintroduce a "compatibility" endpoint that rebuilds the proxy product under a softer name.
