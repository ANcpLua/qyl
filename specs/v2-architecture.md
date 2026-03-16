# qyl v2 — Architecture Specification

> Clean-slate redesign. Evaluated against user UX, product focus, and operational simplicity.
> Supersedes specs 00–10 when accepted.

---

## 1. Product Identity

qyl is an **AI observability platform**. It answers two questions:

1. **What happened?** — traces, logs, metrics, GenAI spans
2. **What did it cost?** — token counts × pricing = cost per call, session, service, model

qyl **observes**. It does not **control**. It sits on the side channel. If qyl goes down, apps keep working — they just lose telemetry. This is the foundational reliability contract.

### 1.1 What qyl is NOT

| Not this | Why |
|----------|-----|
| LLM proxy gateway | Critical-path dependency. See `decisions/no-proxy.md`. |
| Runtime middleware platform | Semantic caching, PII redaction, rate limiting, content moderation, provider failover — each is its own product category. qyl observes what these systems do; it is not these systems. |
| Agent framework | Microsoft.Extensions.AI and Microsoft.Agents.AI exist. qyl instruments agents, it doesn't build them. |
| Workflow engine | Workflow orchestration is deferred. If Loom needs DAG execution, it adopts an existing engine (Elsa Workflows, Durable Task Framework) or builds its own. qyl records workflow executions as traces. |

### 1.2 Single-sentence test

> "Does this feature help the user **see** what their AI app did and what it cost?"

If yes → it belongs in qyl.
If no → it belongs somewhere else.

---

## 2. Architecture

### 2.1 Deployment model

Single Docker image. Single process. No external dependencies.

```text
docker run -p 5100:5100 -p 4317:4317 -p 4318:4318 ghcr.io/ancplua/qyl
```

Ports:
- `:5100` — dashboard + REST API + SSE
- `:4317` — gRPC OTLP ingest
- `:4318` — HTTP OTLP ingest

Environment:
- `QYL_PORT` (default 5100)
- `QYL_GRPC_PORT` (default 4317, 0=disable)
- `QYL_OTLP_PORT` (default 4318, 0=disable)
- `QYL_DATA_PATH` (default qyl.duckdb)
- `QYL_RETENTION_DAYS` (default 0=disabled)

### 2.2 System diagram

```text
┌─────────────────────────────────────────────────┐
│  Apps / Agents / Services                       │
│  (.NET, Python, TypeScript, Go, Java, etc.)     │
└────────┬────────────────────────┬───────────────┘
         │ OTLP (gRPC :4317)     │ OTLP (HTTP :4318)
         ▼                       ▼
┌─────────────────────────────────────────────────┐
│  qyl (single process)                           │
│                                                 │
│  ┌──────────┐  ┌──────────┐  ┌───────────────┐  │
│  │ Ingest   │→ │ DuckDB   │← │ REST API      │  │
│  │ gRPC+HTTP│  │ Storage  │  │ + SSE         │  │
│  └──────────┘  └──────────┘  └───────┬───────┘  │
│                                      │          │
│  ┌───────────────────────────────────┘          │
│  │                                              │
│  │  ┌──────────────┐  ┌────────────────────┐    │
│  └─→│ Dashboard    │  │ Cost Engine        │    │
│     │ (static)     │  │ (tokens × pricing) │    │
│     └──────────────┘  └────────────────────┘    │
└─────────────────────────────────────────────────┘

Internal assemblies (single host, strict acyclic references):

┌──────────────┐
│   qyl.web    │  composition root, API host, SSE, dashboard
└─┬────┬────┬──┘
  │    │    │
  ▼    ▼    ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ qyl.collector│  │  qyl.agents  │  │   qyl.mcp    │
│ OTLP ingest  │  │ Loom/autofix │  │ tool surface │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │                 │                 │
       └──────────┬──────┴──────┬──────────┘
                  ▼             ▼
              ┌────────────────────┐
              │      qyl.core      │
              │ interfaces + DTOs  │
              └─────────┬──────────┘
                        ▼
              ┌────────────────────┐
              │ qyl.infrastructure │
              │ DuckDB + GitHub    │
              └─────────┬──────────┘
                        ▼
                      DuckDB
```

### 2.3 Component boundaries

```text
Tier 1 — The Product (ships as one image):
├── qyl.web             Composition root, REST API, SSE, dashboard host
├── qyl.collector       OTLP ingest and telemetry processors
├── qyl.agents          Loom, autofix, triage, summarization
├── qyl.mcp             MCP tool surface library
├── qyl.infrastructure  DuckDB + external integration implementations
└── qyl.core            Interfaces, DTOs, value objects, query contracts

Cross-cutting:
├── qyl SDK             NuGet package: Roslyn generators, one-line setup
└── qyl dashboard       React UI served by `qyl.web`

Hard rule:
└── No sibling project references. `qyl.web` may see all feature assemblies; all others depend only on `qyl.core`.
```

---

## 3. Tier 1: The Product

### 3.1 qyl server

**Responsibility:** Accept OTLP telemetry, store it, serve it, compute cost.

**Domains (exhaustive — nothing else belongs here):**

| Domain | What it does |
|--------|-------------|
| **Ingest** | gRPC and HTTP OTLP receivers. Converts proto to internal model. |
| **Storage** | DuckDB schema, migrations, read/write. Single-writer, multi-reader. |
| **Query** | REST API for traces, spans, logs, metrics, sessions, services. Pagination. Filtering. |
| **Realtime** | SSE streaming for live telemetry. |
| **Cost** | Token counts × pricing table = cost aggregations. Budget alerts. |
| **Services** | Service registry. Auto-discovered from incoming telemetry. |
| **Health** | Health checks, storage stats, version info. |
| **Auth** | Token-based API authentication. |
| **Schema** | Schema versioning, migrations, promoted columns. |

**Domains that DO NOT belong in `qyl.collector`:**

| Domain | Where it goes |
|--------|--------------|
| Autofix orchestration | qyl.agents |
| Code review | qyl.agents |
| Agent runs / handoffs | qyl.agents |
| GitHub webhooks | qyl.mcp or infrastructure-backed endpoint layer |
| Coding agent provider | Kill |
| Claude Code hooks | Kill |
| Copilot / AG-UI | Kill (or qyl-loom if needed) |
| Workflow execution | Deferred (agent-owned if needed) |
| Build failure tracking | Kill |
| Console bridge | Kill |
| Regression detection | qyl.agents |
| Triage pipeline | qyl.agents |
| Issue analytics | qyl.agents |

**Hard constraint:** The server has no LLM dependencies. It does not call OpenAI, Anthropic, Copilot, or any AI provider. It receives telemetry and serves data. Period.

### 3.2 qyl dashboard

**Responsibility:** Visual interface for exploring telemetry and understanding cost.

**Core views:**

| View | Purpose |
|------|---------|
| **Overview** | Health, request volume, error rate, cost summary |
| **Traces** | Trace list → trace detail → span detail |
| **GenAI** | LLM calls: model, tokens, latency, cost per call |
| **Logs** | Structured log search and exploration |
| **Cost** | Per-model, per-service, per-session cost. Trends. Budget alerts. |
| **Services** | Service map, dependencies, health |
| **Alerts** | Anomaly detection, threshold alerts |
| **Settings** | Pricing table, retention, API keys |

**Views that DO NOT belong in the dashboard:**

| View | Where it goes |
|------|--------------|
| Loom investigation | qyl-loom (separate UI or embedded via MCP) |
| Code review results | qyl-loom |
| Fix runs | qyl-loom |
| Issue triage | qyl-loom |
| Copilot panel | Kill |
| Workflow runs | Kill |
| Bot conversation | Kill |

**Tech stack:**
- React 19, Vite 7, Tailwind CSS 4
- Base UI primitives (never Radix)
- ECharts for dense telemetry, Recharts for light dashboards
- TanStack Table + TanStack Virtual for data grids

### 3.3 qyl SDK

**Responsibility:** Make it trivial for .NET apps to emit OTel telemetry to qyl.

**User experience (the entire API surface):**

```csharp
// Program.cs — one line
builder.AddQyl();
```

```csharp
// Any service — attribute-driven instrumentation
[Traced]
public async Task<Order> ProcessOrder(int orderId) { ... }

[GenAi]
public async Task<string> Summarize(string text) { ... }

[Db]
public async Task<List<User>> GetActiveUsers() { ... }
```

That's it. The Roslyn source generators produce compile-time interceptors. The runtime wiring discovers the qyl server and configures OTLP export. Zero reflection. Zero runtime cost until observed.

**What the SDK contains:**

| Component | What it does |
|-----------|-------------|
| `[Traced]` attribute + generator | Compile-time span instrumentation |
| `[GenAi]` attribute + generator | GenAI semconv 1.40 span instrumentation |
| `[Db]` attribute + generator | Database call instrumentation |
| `[Counter]` attribute + generator | Metrics instrumentation |
| `InstrumentedChatClient` | `DelegatingChatClient` that emits GenAI spans |
| Collector discovery | Auto-detect qyl server on the network |
| OTLP export wiring | Configure OTel SDK to export to discovered server |

**What the SDK DOES NOT contain:**

| Not this | Why |
|----------|-----|
| Semantic cache | Not observability. Separate middleware. |
| Rate limiting | Not observability. Separate middleware. |
| PII redaction | Not observability. Separate middleware. |
| Content guards | Not observability. Separate middleware. |
| Provider fallback | Not observability. Separate middleware. |
| Agent builder | Microsoft.Agents.AI exists. |
| Chunking pipeline | Not the SDK's problem. |
| Copilot adapter | Not the SDK's problem. |

**NuGet package name:** `qyl` (single package, simple)

**Target frameworks:** `net10.0`

### 3.4 Cost engine (server-side)

Cost is a first-class feature, not a subsection. It requires zero middleware, zero proxy, zero SDK changes — it's pure server-side computation from data qyl already stores.

**Data source:** GenAI spans contain `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `gen_ai.request.model`, `gen_ai.system`.

**Pricing table:** Stored in DuckDB. Configurable via REST API and MCP tools.

```sql
CREATE TABLE model_pricing (
    provider       VARCHAR NOT NULL,  -- 'openai', 'anthropic', etc.
    model          VARCHAR NOT NULL,  -- 'gpt-4o', 'claude-sonnet-4-6', etc.
    input_cost     DECIMAL NOT NULL,  -- cost per 1M input tokens
    output_cost    DECIMAL NOT NULL,  -- cost per 1M output tokens
    reasoning_cost DECIMAL,           -- cost per 1M reasoning tokens (o-series, NULL if N/A)
    cache_read_cost  DECIMAL,         -- cost per 1M cached input tokens (NULL if N/A)
    cache_write_cost DECIMAL,         -- cost per 1M cache write tokens (NULL if N/A)
    valid_from     TIMESTAMP NOT NULL,
    valid_to       TIMESTAMP,         -- NULL = current pricing
    PRIMARY KEY (provider, model, valid_from)
);

-- Tiered pricing for volume-based models (e.g., Anthropic batch tiers)
CREATE TABLE model_pricing_tiers (
    provider         VARCHAR NOT NULL,
    model            VARCHAR NOT NULL,
    tier_name        VARCHAR NOT NULL,  -- 'standard', 'batch', 'volume_1m+'
    input_cost       DECIMAL NOT NULL,
    output_cost      DECIMAL NOT NULL,
    reasoning_cost   DECIMAL,
    min_tokens       BIGINT,            -- threshold to activate this tier (NULL = default)
    valid_from       TIMESTAMP NOT NULL,
    PRIMARY KEY (provider, model, tier_name, valid_from)
);
```

**Seed data strategy:**

qyl ships a bundled `model-pricing.json` with current pricing for the top 30 models across OpenAI, Anthropic, Google, Meta, and Mistral. On first boot, if `model_pricing` is empty, the seed data auto-loads. This ensures cost tracking works immediately without manual configuration.

- Seed file: `data/model-pricing.json` (checked into repo, updated with releases)
- Sync endpoint: `POST /api/v1/cost/sync-pricing` — pulls latest pricing from a community-maintained registry
- Manual override: `PUT /api/v1/cost/pricing/{provider}/{model}` — per-model pricing override
- Dashboard: Settings → Pricing shows current pricing with last-updated timestamps

Without seed data, cost tracking is dead on arrival for new users. This is the #1 UX lesson from Langfuse (ships pricing for popular models) and Helicone (ships 300+ model prices).

**Aggregations (REST API + dashboard):**

| Aggregation | Endpoint |
|-------------|----------|
| Cost per call | `GET /api/v1/genai/spans?include=cost` |
| Cost per session | `GET /api/v1/cost/by-session` |
| Cost per service | `GET /api/v1/cost/by-service` |
| Cost per model | `GET /api/v1/cost/by-model` |
| Cost over time | `GET /api/v1/cost/timeseries` |
| Budget status | `GET /api/v1/cost/budget` |

**Budget alerts:** Configurable spend threshold per service/model. When exceeded, emit alert (SSE event + optional webhook).

---

## 4. Tier 2: Extensions

### 4.1 qyl-mcp

MCP server for AI-native telemetry queries. Separate process. Communicates with qyl server via HTTP.

**Deployment modes:**
- `stdio` — local, for Claude Code / desktop tools
- Streamable HTTP (`/mcp`) — remote, for Anthropic directory

**Tool surface (focused):**

| Category | Tools |
|----------|-------|
| Discovery | list-services, get-service-map, list-projects |
| Traces | search-traces, get-trace, get-span |
| Logs | search-logs |
| Metrics | list-metrics, query-metrics |
| GenAI | search-genai-spans, get-cost-summary |
| Sessions | search-sessions, get-session |
| Management | configure-retention, manage-pricing, create-api-key |

**Hard constraints:**
- Every tool has `readOnlyHint` / `destructiveHint` annotations
- Every response < 25,000 tokens
- All list endpoints paginated
- OAuth 2.1 + DCR for remote mode
- No direct DuckDB access — HTTP to qyl server only

### 4.2 qyl.agents

AI investigation and automation layer. Library mounted by `qyl.web`.

**Responsibility:** Given an error/anomaly, investigate root cause using AI agents with access to telemetry and artifacts through `qyl.core` interfaces.

**Dependencies:**
- `qyl.core` only
- LLM providers (for AI investigation)
- Microsoft.Extensions.AI / Microsoft.Agents.AI (for agent construction)

**Architecture rules:**
- `qyl.agents` depends on `qyl.core`, never on `qyl.collector`, `qyl.mcp`, or `qyl.infrastructure`
- All telemetry, artifact, and GitOps access goes through `ITelemetryStore`, `IArtifactService`, `IIssueService`, `IGitOpsService`, and related `qyl.core` contracts
- `Microsoft.Extensions.AI` lives only here
- Loom is a namespace/domain inside `qyl.agents`, not a separate runtime product
- If agent workflows need DAG orchestration later, that dependency remains owned by `qyl.agents`

---

## 5. Kill List

Components deleted from qyl. Not deprecated — deleted.

| Component | Files | Rationale |
|-----------|-------|-----------|
| **standalone qyl.loom** | legacy split | Fold Loom behavior into `qyl.agents`; do not preserve a separate runtime/library boundary for the same domain. |
| **qyl.workflows** | `src/qyl.workflows/` | Linear-only engine with no branching, parallelism, retries, or timeouts. Fundamental gaps, not incremental. If agents need workflows, agents own the dependency. |
| **qyl.hosting** | `src/qyl.hosting/` | The collector IS the host. QylApp/QylAppBuilder is an abstraction over ASP.NET Core's own builder. |
| **qyl.watch** | `src/qyl.watch/` | Terminal span viewer. Nice toy, not core product. Publish as standalone dotnet tool if desired. |
| **qyl.browser** | `src/qyl.browser/` | Web Vitals for AI apps. Niche. Revisit when demand exists. |
| **GenAI middleware** | spec 08 sections 3.2–3.6 | Cache, rate limit, PII, guards, fallback — each is its own product. qyl observes, doesn't control. |
| **Collector: Autofix/** | `src/qyl.collector/Autofix/` | Moves to qyl-loom. |
| **Collector: CodingAgent/** | `src/qyl.collector/CodingAgent/` | Kill entirely. |
| **Collector: ClaudeCode/** | `src/qyl.collector/ClaudeCode/` | Kill entirely. |
| **Collector: Copilot/** | `src/qyl.collector/Copilot/` | Kill AG-UI integration from collector. |
| **Collector: BuildFailures/** | `src/qyl.collector/BuildFailures/` | Kill. |
| **Collector: ConsoleBridge/** | `src/qyl.collector/ConsoleBridge/` | Kill. |
| **Collector: Identity/** | `src/qyl.collector/Identity/` | GitHub service moves to qyl-loom. |
| **Collector: Workflow/** | `src/qyl.collector/Workflow/` | Workflow execution is not the server's job. |
| **Collector: Insights/** | `src/qyl.collector/Insights/` | AI-generated insights move to qyl-loom. |

### 5.1 What stays in the collector

After the kill list, the collector contains:

```
src/qyl.collector/
├── Auth/           Token authentication
├── Grpc/           gRPC OTLP ingest
├── Health/         Health checks
├── Ingestion/      OTLP → internal model conversion
├── Mapping/        Attribute mapping
├── Observe/        Subscription manager, catalog, schema negotiation
├── Query/          REST API for traces, logs, metrics, spans
├── Realtime/       SSE streaming
├── SchemaControl/  Schema versioning
├── Search/         Full-text search
├── Services/       Service registry
├── Storage/        DuckDB schema, migrations, read/write
├── Telemetry/      Self-instrumentation
├── Analytics/      Anomaly detection (statistical, no LLM)
├── Cost/           Token × pricing aggregations (NEW)
├── Dashboard/      Static file serving
├── Endpoints/      MCP-facing REST endpoints
├── Errors/         Error grouping and issue tracking
├── Meta/           System metadata
├── Program.cs      Entry point
└── *.csproj
```

~15 focused domains, down from ~30.

---

## 6. Naming

| Current name | New name | Rationale |
|-------------|----------|-----------|
| qyl.collector | **qyl** (the server binary) | It's the product, not a "collector" |
| qyl.servicedefaults | **qyl** (NuGet package) | "servicedefaults" is an Aspire-ism |
| qyl.instrumentation | Fold into **qyl** package | No need for two packages |
| qyl.instrumentation.generators | Internal to **qyl** package | Implementation detail |
| qyl.collector.storage.generators | Internal to **qyl** server | Implementation detail |
| qyl.contracts | **qyl.contracts** (unchanged) | "contracts" is the standard .NET term for shared DTOs/types. "protocol" implies wire format, which is misleading. |
| qyl.dashboard | **qyl-ui** | Shorter, clearer |
| standalone qyl.loom | Deleted | Folded into `qyl.agents` |
| qyl.workflows | Deleted | — |
| qyl.hosting | Deleted | — |
| qyl.watch | Deleted (or `qyl-watch` standalone tool) | — |
| qyl.browser | Deleted | — |

### 6.1 Solution structure after rename

```text
qyl.slnx
├── src/
│   ├── qyl/                          # The server (was qyl.collector)
│   ├── qyl.contracts/                 # Shared types (unchanged)
│   ├── qyl.sdk/                      # NuGet package (was qyl.instrumentation)
│   ├── qyl.sdk.generators/           # Roslyn generators (internal to SDK)
│   ├── qyl.storage.generators/       # DuckDB generators (internal to server)
│   ├── qyl-ui/                       # React dashboard (was qyl.dashboard)
│   └── qyl-mcp/                      # MCP server (unchanged)
├── tests/
│   ├── qyl.tests/                    # Server tests
│   └── qyl.sdk.tests/               # SDK tests
├── core/
│   └── specs/                        # TypeSpec definitions
├── eng/
│   └── build/                        # NUKE build
└── specs/                            # Architecture specs
```

---

## 7. User Experience Requirements

### 7.1 Setup: 2 minutes or less

**Server:**
```bash
docker run -p 5100:5100 -p 4317:4317 -p 4318:4318 ghcr.io/ancplua/qyl
```

**SDK (.NET):**
```bash
dotnet add package qyl
```

```csharp
builder.AddQyl();
```

No configuration file. No environment variables (auto-discovers local qyl via well-known port). No explicit OTLP endpoint unless running remote.

### 7.2 Time to first trace: 30 seconds

After `builder.AddQyl()`, HTTP calls and `[Traced]` methods appear in the dashboard automatically. No manual span creation required.

### 7.3 Time to first GenAI insight: 1 minute

After wrapping an `IChatClient` with `UseQylInstrumentation()` or annotating with `[GenAi]`, the GenAI view shows model, tokens, latency, and cost.

### 7.4 Non-.NET apps: 5 minutes

Any language with an OTel SDK can emit traces to qyl's OTLP endpoint. The dashboard shows the same views regardless of source language.

```python
# Python example
from opentelemetry.sdk.trace.export import OTLPSpanExporter
exporter = OTLPSpanExporter(endpoint="http://localhost:4318")
```

### 7.5 MCP: instant

```bash
# Claude Code
claude mcp add qyl -- dotnet run --project src/qyl-mcp

# Or remote
claude mcp add qyl --url https://mcp.qyl.info/mcp
```

Then: "What failed in the last hour?" → Claude queries qyl, answers with evidence.

---

## 8. Tech Stack (unchanged)

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10.0 LTS, C# 14, net10.0 |
| Frontend | React 19, Vite 7, Tailwind CSS 4 |
| Storage | DuckDB (columnar, glibc required) |
| Protocol | OTel Semantic Conventions 1.40 |
| Testing | xUnit v3, Microsoft Testing Platform |
| Build | NUKE |

### 8.1 Banned APIs (unchanged)

`DateTime.Now`, `Newtonsoft.Json`, `object _lock`, `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`, `ISourceGenerator`, `SyntaxFactory.NormalizeWhitespace()`, runtime reflection, `dynamic`, `.Result`, `.Wait()`

---

## 9. Dependency Rules

```text
qyl.contracts   → nothing (BCL-only, zero packages)
qyl.sdk         → qyl.contracts
qyl.sdk.generators → Roslyn APIs only (no runtime references)
qyl (server)    → qyl.contracts, DuckDB, ASP.NET Core, OTel SDK
qyl-ui          → qyl (HTTP at runtime)
qyl-mcp         → qyl.contracts, ModelContextProtocol SDK
qyl-mcp         → qyl (HTTP at runtime, never ProjectReference)
qyl-loom        → qyl (HTTP at runtime)
qyl-loom        → Microsoft.Extensions.AI, Microsoft.Agents.AI
```

**Forbidden:**
- qyl (server) → qyl-loom (one-way dependency: loom → qyl)
- qyl-mcp → qyl (ProjectReference — HTTP only)
- qyl.contracts → any NuGet package
- qyl (server) → any LLM provider SDK
- qyl.sdk → qyl (server) (SDK is client-side, no server dependency)

---

## 10. Migration Path

This is not a refactor. It is a rebuild with knowledge transfer.

### 10.1 What transfers (code or knowledge)

| From | To | Transfer type |
|------|-----|---------------|
| `qyl.collector/Ingestion/` | qyl server | Code (adapt) |
| `qyl.collector/Storage/` | qyl server | Code (adapt) |
| `qyl.collector/Query/` | qyl server | Code (adapt) |
| `qyl.collector/Realtime/` | qyl server | Code (adapt) |
| `qyl.collector/Analytics/` | qyl server | Code (adapt) |
| `qyl.collector/Health/` | qyl server | Code (adapt) |
| `qyl.collector/Services/` | qyl server | Code (adapt) |
| `qyl.collector/Errors/` | qyl server | Code (adapt) |
| `qyl.collector/Observe/` | qyl server | Code (adapt) |
| `qyl.instrumentation/` | qyl SDK | Code (adapt) |
| `qyl.instrumentation.generators/` | qyl SDK generators | Code (adapt) |
| `qyl.contracts/` | qyl.contracts | Code (unchanged, publish as NuGet) |
| `qyl.dashboard/` | qyl-ui | Code (strip non-core views) |
| `qyl.mcp/` | qyl-mcp | Code (strip non-core tools) |
| `qyl.collector/Autofix/` | qyl.agents | Knowledge (rewrite in agents context) |
| `qyl.agents/InstrumentedChatClient` | qyl SDK | Code (single file) |
| `specs/08-genai-controls.md` | This spec (section 3.4) | Knowledge (cost engine only) |
| DuckDB schema + migrations | qyl server | Code (adapt) |
| TypeSpec definitions | qyl.contracts | Code (unchanged) |
| NUKE build | eng/build | Code (adapt targets) |

### 10.2 What does NOT transfer

Everything on the kill list (section 5). No code, no "maybe later," no commented-out stubs.

---

## 11. Explicitly Out of Scope

These features exist in competitors but are deliberately excluded from qyl v2. This is not a gap — it is a scope decision. Each may become a Tier 2 extension in future versions.

| Feature | Competitors that have it | Why excluded |
|---------|-------------------------|--------------|
| **Evaluation / LLM-as-judge** | Langfuse, LangSmith, Arize Phoenix | Evaluation is a separate product category (testing, not observability). qyl records evaluation results as traces if the user runs evaluations through their own tooling. |
| **Prompt management / versioning** | Langfuse, LangSmith, Helicone | Prompt management is a development workflow feature, not an observability feature. IDEs, version control, and LLM frameworks handle this. |
| **Python / TypeScript / Go SDKs** | All competitors ship first-party SDKs | qyl is .NET-first. Non-.NET apps use their language's OTel SDK with GenAI semconv. First-party SDKs for other languages are a future Tier 2 opportunity if demand justifies the maintenance burden. |
| **Data export / OTLP re-export** | Expected by users with existing observability stacks | Deferred. When needed, implement as an OTLP exporter that forwards a filtered subset of ingested data to another backend (Grafana, Datadog, etc.). |
| **Semantic caching** | Helicone (proxy-side) | Not observability. Caching is a runtime control. If a user's app uses caching, qyl shows cache hit/miss as span events. |
| **PII redaction** | Helicone (proxy-side) | Not observability. PII is a compliance concern. If a user's app scrubs PII, qyl records the redaction event. |
| **Runtime rate limiting** | Helicone (proxy-side) | Not observability. Rate limiting is a runtime control. qyl shows rate limit events in spans. |

### 11.1 Open Questions

| # | Question | Impact | Status |
|---|----------|--------|--------|
| 1 | Should `qyl.contracts` be a published NuGet package? | If external consumers (custom MCP tools, third-party integrations) need shared types. Recommendation: YES — publish it, following `Microsoft.Extensions.AI.Abstractions` pattern. | **Needs decision** |

## 12. Dashboard Performance Architecture

The acceptance criteria claim "dashboard loads in < 1 second with 100K spans." This section specifies how.

### 12.1 Server-side pre-aggregation

DuckDB materialized views for expensive aggregations that the dashboard hits on every page load:

```sql
-- Cost rollups (refreshed on ingest batch or every 60s)
CREATE OR REPLACE VIEW cost_by_model_hourly AS
SELECT
    date_trunc('hour', start_time) AS bucket,
    resource_service_name AS service,
    gen_ai_request_model AS model,
    gen_ai_system AS provider,
    COUNT(*) AS call_count,
    SUM(gen_ai_usage_input_tokens) AS total_input_tokens,
    SUM(gen_ai_usage_output_tokens) AS total_output_tokens,
    SUM(gen_ai_usage_input_tokens * p.input_cost / 1000000.0
      + gen_ai_usage_output_tokens * p.output_cost / 1000000.0) AS total_cost
FROM spans s
LEFT JOIN model_pricing p ON s.gen_ai_system = p.provider
    AND s.gen_ai_request_model = p.model
    AND p.valid_to IS NULL
WHERE gen_ai_request_model IS NOT NULL
GROUP BY ALL;

-- Service health rollups
CREATE OR REPLACE VIEW service_health_hourly AS
SELECT
    date_trunc('hour', start_time) AS bucket,
    resource_service_name AS service,
    COUNT(*) AS span_count,
    COUNT(*) FILTER (WHERE status_code = 'ERROR') AS error_count,
    AVG(duration_ms) AS avg_latency_ms,
    PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ms) AS p99_latency_ms
FROM spans
GROUP BY ALL;
```

### 12.2 Query patterns

| Dashboard view | Query strategy | Target latency |
|----------------|---------------|----------------|
| Overview | Pre-aggregated hourly rollups | < 50ms |
| Traces list | Cursor-based pagination, LIMIT 50, indexed by start_time DESC | < 100ms |
| Trace detail | Single trace by ID, all spans | < 50ms |
| GenAI view | Pre-aggregated cost rollups + recent spans list | < 100ms |
| Cost dashboard | Pre-aggregated timeseries from `cost_by_model_hourly` | < 50ms |
| Logs | Cursor-based pagination, full-text search via DuckDB `CONTAINS` | < 200ms |

### 12.3 Pagination strategy

All list endpoints use cursor-based pagination (not offset-based):

```
GET /api/v1/traces?cursor={last_trace_start_time}&limit=50
```

Cursor = `start_time` of the last item. Stable under concurrent inserts. No COUNT(*) for total — show "load more" instead of page numbers.

---

## 13. Acceptance Criteria

### 13.1 Product criteria

- [ ] `docker run qyl` starts a working server with dashboard in < 5 seconds
- [ ] `dotnet add package qyl` + `builder.AddQyl()` produces traces in the dashboard
- [ ] GenAI spans show model, tokens, latency, and computed cost
- [ ] Cost dashboard shows per-model, per-service, per-session aggregations
- [ ] Non-.NET app emitting OTel GenAI spans appears in the same dashboard
- [ ] MCP server answers "what failed in the last hour?" from qyl data
- [ ] Server has zero LLM provider dependencies
- [ ] Server process survives qyl-loom being offline

### 13.2 Architecture criteria

- [ ] Collector contains ≤ 15 domain directories (down from 30+)
- [ ] No code path in the server calls an LLM provider
- [ ] SDK contains exactly one `DelegatingChatClient` (`InstrumentedChatClient`)
- [ ] SDK contains zero middleware beyond instrumentation
- [ ] qyl-mcp communicates with server via HTTP only (no ProjectReference)
- [ ] qyl.contracts has zero NuGet package dependencies
- [ ] No component on the kill list exists in the codebase

### 13.3 UX criteria

- [ ] Setup time (server + SDK + first trace) ≤ 2 minutes
- [ ] Dashboard loads in < 1 second with 100K spans in DuckDB
- [ ] Cost view answers "how much did I spend today?" in one click
- [ ] GenAI view answers "which model is slowest?" in one click

---

## 14. Decision Record

| Decision | Rationale | Alternatives rejected |
|----------|-----------|----------------------|
| No proxy | Side channel reliability contract. Provider API maintenance burden. | Helicone sidecar (acquired, legacy attributes), custom proxy (scope creep) |
| No GenAI middleware (except instrumentation) | Each control is its own product category. Observability ≠ control. | Full middleware pipeline (spec 08 v1) |
| Cost as first-class feature | Pure server-side computation from existing data. No proxy needed. Helicone-killer. | Cost as middleware (requires SDK changes), cost as separate service (unnecessary) |
| Keep qyl.agents as AI boundary | All `Microsoft.Extensions.AI` usage must live in one place outside collector and MCP. | Spread AI logic across collector/web/mcp (recreates God-process coupling) |
| Kill qyl.workflows | Linear-only engine is a dead end. If Loom needs DAG workflows, Loom owns the dependency. | Extend DeclarativeEngine (spec 09 known gaps are fundamental, not incremental) |
| Server has no LLM dependencies | Keeps the server focused. AI investigation belongs in Loom. | Embedded agent in server (creates LLM dependency, resource contention) |
| Keep `qyl.contracts` name | "contracts" is the standard .NET term for shared DTOs/types between client and server. "protocol" implies wire format. | Rename to `qyl.protocol` (misleading — contents are DTOs, not a protocol spec) |
| Publish `qyl.contracts` as NuGet package | External consumers (custom MCP tools, third-party integrations) need shared types. Follows `Microsoft.Extensions.AI.Abstractions` pattern. | Keep internal-only (blocks ecosystem growth) |
| Ship bundled model pricing | Empty pricing table on first boot is dead UX. Langfuse ships popular model prices. Helicone ships 300+. | Manual-only pricing (forces configuration before cost tracking works) |
| Workflow orchestration deferred | No real DAG engine exists in the ecosystem that fits. Linear-only DeclarativeEngine is a dead end. If Loom needs DAGs, Loom owns it. | Build custom DAG engine (scope creep), adopt Temporal (too heavy for single-process) |
