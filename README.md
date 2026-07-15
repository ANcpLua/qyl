# qyl

qyl is an OpenTelemetry-compatible observability product for ingesting,
investigating, and visualizing telemetry across all four OTLP signals — traces,
logs, metrics, and profiles — with GenAI cost intelligence on top: provider
billing synchronization, the live OpenRouter model-pricing catalog, and the
GenAI ETL audit.
The product is the collector, DuckDB storage, public investigation API,
dashboard, and local distributed-app host. OpenTelemetry supplies the ingestion
protocol and telemetry vocabulary; it is not Qyl's product API.

qyl is pre-beta. Unpublished surfaces converge directly on a small, executable
product; compatibility requires a proven consumer.

## Architecture

```text
OpenTelemetry semantic conventions
        |
        v
Qyl.OpenTelemetry.SemanticConventions    typed vocabulary
        |
        v
qyl-api-schema                           TypeSpec product contract
        |
        +----> Qyl.Api.Contracts         generated .NET contracts
        +----> generated TS contracts    dashboard and other consumers
        |
        v
qyl.collector                            OTLP ingest (traces, logs, metrics,
        |                                profiles) -> DuckDB -> product API;
        |                                provider billing sync + live pricing
        |                                catalog -> GenAI ETL audit
        v
qyl.dashboard                            investigation UI, including the Cost page
```

The main components are:

| Path | Responsibility |
| --- | --- |
| `services/qyl.collector` | Official OTLP wire ingestion (all four signals), DuckDB storage, the generated-contract product API, and the GenAI cost subsystem (provider billing sync, model-pricing catalog, ETL audit) |
| `services/qyl.dashboard` | React investigation interface backed by generated client contracts |
| `internal/qyl.instrumentation` | Qyl service defaults and Qyl-specific telemetry |
| `internal/qyl.instrumentation.generators`, `internal/qyl.collector.storage.generators` | Compile-time source generators for instrumentation and storage |
| `packages/Qyl.Host` | Unpublished local distributed-app host: fluent AppHost-equivalent with subprocess orchestration and deferred endpoint resolution, no Aspire dependencies |
| `packages/Qyl.Host.Console` | Host console frontend consuming the generated TypeScript contracts (build/typecheck-gated) |
| `packages/Qyl.Host.Mcp` | Optional MCP hosting integration for the runner (stdio/HTTP/in-process resources, OTLP export of the MCP SDK ActivitySource) |
| `packages/Qyl.Run.Host` | Composed local host entry point: the collector plus its isolated diagnostics collector |
| `packages/Qyl.Run.Workload` | Synthetic GenAI workload emitter for local end-to-end exercise |
| `eng/build` | Build, generation, verification, and packaging gates |

## Ingestion and product API

OTLP ingestion speaks the official protobuf contract on gRPC (`:4317` —
trace, logs, metrics, and profiles services) and HTTP (`POST /v1/traces`,
`/v1/logs`, `/v1/metrics`, plus `POST /v1development/profiles` for the
development profiles signal).

The generated product API is served under `/api/v1`:

| Route group | Purpose |
| --- | --- |
| `/sessions`, `/sessions/stats`, `/sessions/{id}`, `/sessions/{id}/traces` | Session inventory, statistics, and session-scoped traces |
| `/traces`, `/traces/{id}`, `/traces/{id}/spans` | Trace inventory and full span data |
| `/logs`, `/stream/logs` | Log search and live log streaming |
| `/cost/etl-audit`, `/cost/etl-audit/evaluate` | GenAI ETL audit report and scenario evaluation (detailed below) |
| `/profiles`, `/profiles/{id}`, `/profiles/by-trace/{id}`, `/profiles/by-span/{id}` | Profile inventory and correlation lookups |

## Contract boundaries

Every client-visible Qyl request, response, stream event, and error is authored in
the external [`qyl-api-schema`](https://github.com/ANcpLua/qyl-api-schema) TypeSpec
repository. Qyl consumes the generated `Qyl.Api.Contracts` package and generated
TypeScript contract artifacts; it does not maintain a second public DTO model in the collector.

OTLP endpoints use the official OpenTelemetry protobuf contract. Collector storage
rows, ingest batches, query models, and internal projections remain implementation
details and are explicitly mapped to generated product contracts before crossing an
HTTP, gRPC, MCP, streaming, or client boundary.

## Instrumentation

[`Qyl.OpenTelemetry.AutoInstrumentation`](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation)
provides the managed compile-time instrumentation substrate. It uses Roslyn source
interceptors, build assets, BCL telemetry primitives, and public diagnostic hooks;
it does not use a CLR profiler or runtime IL rewriting. Its generated
[`coverage-matrix.md`](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation/blob/main/docs/coverage-matrix.md)
is the detailed capability and evidence record. Qyl does not duplicate that matrix
or turn configuration-only rows into runtime-coverage claims.

## GenAI ETL audit

The GenAI ETL audit groups repeated calls by privacy-safe workflow dimensions,
reports their observable token, latency, error, provider-billing, and live-catalog
evidence, and proposes lower-risk implementation classes as hypotheses. It does
not treat a hypothesis as a proven replacement.

The generated product API exposes two operations:

| Operation | Purpose |
| --- | --- |
| `GET /api/v1/cost/etl-audit` | Report workflow clusters and available evidence. Optional `startTime`, `endTime`, and `limit` query parameters select the period and top clusters; `limit` defaults to 25 and is capped at 100. |
| `POST /api/v1/cost/etl-audit/evaluate` | Evaluate 1–100 explicit replacement scenarios for the same optional `startTime` and `endTime` period. |

Both operations accept the optional `X-Qyl-Project` project-scope header. With
dates omitted, the collector uses the previous 30 completed UTC days, ending at
the latest UTC midnight; explicit periods are capped at 180 days. For each scenario,
the request identifies a `cluster_id`
and supplies coverage, alternative cost, period maintenance cost, period error
cost, and optionally an explicit frontier cost. The exact calculation is:

```text
replaceable_value_usd = calls * coverage *
  (frontier_cost_per_call_usd - alternative_cost_per_call_usd) -
  period_maintenance_cost_usd - period_error_cost_usd
```

Negative values are retained. An explicit `frontier_cost_per_call_usd` wins when
present. Otherwise the evaluator uses the cluster's calculated current-catalog
token estimate. Provider billing aggregates are never converted into a per-call
baseline; when neither an explicit scenario value nor a calculated catalog token
estimate exists, the result is `missing_frontier_cost` rather than zero.

Every cluster reports all six promotion gates: contract stability, offline replay,
calibrated confidence, shadow traffic, limited serving, and rollback plus residual
policy. A gate remains `blocked_missing_evidence` until its required evidence is
present; available evidence is reported as `not_evaluated`, never fabricated as a
passing result.

### Provider cost provenance

The collector fetches billed costs only from provider-owned APIs:

| Provider | Official source | Available granularity |
| --- | --- | --- |
| OpenAI | [Organization Costs API](https://developers.openai.com/api/reference/resources/admin/subresources/organization/subresources/usage/methods/costs) | Daily project and line-item aggregates; the API does not identify a model or request. |
| Anthropic | [Cost Report API](https://platform.claude.com/docs/en/manage-claude/usage-cost-api) | Daily workspace aggregates, with model grouping only when the provider response identifies the model. Priority Tier costs are excluded by that API. |

Provider reports are aggregate, daily, and may be delayed. Qyl synchronizes only
completed UTC days and never claims that provider billing data is a per-trace
price. Even when only one workflow is visible for a provider/model, Qyl cannot
prove that the provider scope contains no unobserved calls or non-inference line
items. Provider totals therefore remain source-level and are not allocated to
workflow clusters or used as evaluator baselines.

Configure synchronization with environment variables; credentials are read at
runtime and must not be committed:

| Variable | Meaning |
| --- | --- |
| `QYL_COST_PROJECT_ID` | Qyl project that owns the synchronized cost buckets; defaults to `default`, and audit requests must use the same project scope. |
| `QYL_OPENAI_ADMIN_KEY` | OpenAI organization admin key used only for the official Costs API. |
| `QYL_OPENAI_PROJECT_ID` | Optional exact OpenAI project filter for source-level billing totals. |
| `QYL_ANTHROPIC_ADMIN_KEY` | Anthropic admin key used only for the official Cost Report API. |
| `QYL_ANTHROPIC_WORKSPACE_ID` | Optional exact Anthropic workspace filter for source-level totals; use `default` to select only the provider's default workspace (`workspace_id: null`). |
| `QYL_COST_SYNC_INTERVAL_MINUTES` | Poll interval; defaults to 15 and accepts 1–1440. |
| `QYL_COST_LOOKBACK_DAYS` | Daily report lookback; defaults to 31 and accepts 1–180. |

Without a project or workspace filter, credentials fetch organization-level totals.
Changing a configured provider scope invalidates previously synchronized totals by
a non-secret scope fingerprint, so old-scope rows cannot be reused. An unpriced
cluster keeps its cost fields absent instead of using zero. The audit reports
`unconfigured`, `pending`, `current`, `stale`, and `sync_failed` billing-source
states explicitly.

### Live model catalog token estimates

Catalog pricing is a separate boundary from provider billing. Qyl has no embedded
model/rate table and does not infer prices from model-name prefixes, aliases, or
release-date suffixes. OpenRouter is the sole model-pricing source: Qyl parses its
returned price dimensions and activates a content-addressed immutable snapshot.
Freshness, timeout, and response-size limits are configuration, not model-specific
branches in the audit path.

The collector uses OpenRouter's
[`GET /api/v1/models?output_modalities=all`](https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties)
API so non-text model metadata is not omitted by the endpoint's default filter.
OpenRouter publishes the lowest available rate for a model, so Qyl records
`minimum_available_rate` provenance rather than presenting the result as an
official provider bill or a uniquely routed endpoint price.
`QYL_OPENROUTER_API_KEY` supplies the optional Bearer token. Qyl sends no trace,
prompt, completion, or project data to this endpoint.

Each physical model-call span is evaluated before workflow aggregation. The actual
response model is preferred over the requested model, identities are matched
exactly, and conditional tiers use that call's measured quantities rather than an
average. Input/output totals include cache and reasoning subtypes under the
OpenTelemetry contract. When optional subtype telemetry is absent, its tokens stay
covered by the published base rate and the unapplied adjustment is listed as an
explicit exclusion; absence is never rewritten as observed zero. Positive
non-token meters remain outside the named token estimate unless telemetry proves
they applied, in which case missing usage fails closed.

The resulting `estimated_catalog_token_cost_usd` is a counterfactual using the
currently active catalog snapshot. Its snapshot id, retrieval time, exact matched
model, components, and exclusions travel with the result. Applying that snapshot
to an older audit period is not a claim about the price that applied historically.
Retries appear as their observed physical call spans and request charges are
counted once per span; Qyl does not invent a retry multiplier.

Configure live catalog synchronization with environment variables:

| Variable | Meaning |
| --- | --- |
| `QYL_MODEL_PRICING_SYNC_INTERVAL_MINUTES` | Refresh interval; defaults to 60 and accepts 1–1440. The collector refreshes once at startup and then once per interval. |
| `QYL_MODEL_PRICING_MAX_STALENESS_MINUTES` | Maximum age of the latest successful verification; defaults to three refresh intervals (at least 60 minutes). A stale snapshot cannot price a cluster. |
| `QYL_MODEL_PRICING_RETAINED_SNAPSHOTS` | Maximum immutable OpenRouter catalog snapshots retained; defaults to 32 and accepts 1–1024. Activation and pruning are one storage transaction. |
| `QYL_MODEL_PRICING_HTTP_TIMEOUT_SECONDS` | OpenRouter catalog request timeout; defaults to 30 and accepts 1–300. |
| `QYL_MODEL_PRICING_MAX_RESPONSE_MIB` | Maximum accepted OpenRouter response size; defaults to 16 and accepts 1–64. |
| `QYL_OPENROUTER_API_KEY` | Optional bearer token for the fixed OpenRouter Models API endpoint. |

## Run locally

Requirements: the .NET SDK selected by `global.json` and Node.js `^20.19.0` or `>=22.12.0`.

Start the collector:

```bash
QYL_BIND_ADDRESS=127.0.0.1 QYL_OTLP_AUTH_MODE=Unsecured \
  dotnet run --project services/qyl.collector
```

Start the dashboard in another terminal:

```bash
cd services/qyl.dashboard
npm install
npm run dev
```

The browser dashboard is a development surface served whenever auth is not
`ApiKey`; it binds to loopback by default (`QYL_BIND_ADDRESS` accepts other
addresses, so avoid remote bindings in `Unsecured` mode explicitly) and never
stores the ingest-capable API key. `ApiKey` deployments expose only the generated
product API and OTLP endpoints; use a generated client rather than the dashboard.

Or run the local host, which composes the collector and its isolated diagnostics
collector:

```bash
dotnet run --project packages/Qyl.Run.Host
```

Point an OTLP exporter at the collector:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:5100
export OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

| Port | Purpose |
| --- | --- |
| `5100` | Product API and OTLP/HTTP; embedded dashboard in `Unsecured` mode (loopback by default) |
| `4317` | OTLP/gRPC receiver |
| `4318` | Dedicated OTLP/HTTP receiver |

### Native AOT publish

Native AOT is the collector's publish contract: `QylAot` defaults on, so
`dotnet publish services/qyl.collector` produces a native binary, and the
Dockerfile builds on the AOT SDK image and runs on `runtime-deps`. Publish with
`-p:QylAot=false` for a JIT diagnostic build with full analyzer enforcement.
The native lane's executable owner is `eng/scripts/collector-aot-smoke.sh`.

## Verify

Run the repository gate before pushing:

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

Generated files are outputs, not editing surfaces. Change the owning TypeSpec,
protobuf input, generator, or manifest and regenerate in the same commit.

## License

MIT
