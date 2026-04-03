# qyl — Architecture Specification

> Owner: system
> SSOT: YES (product identity, component boundaries, dependency rules, ownership boundaries)
> Depends on: none
> Used by: all subsystem specs

Kernel spec. Defines what qyl is, what it is not, component boundaries, and invariants. Subsystem detail lives in dedicated specs — not here.

---

## 1. Product Identity

qyl is an **AI observability platform**. It answers two questions:

1. **What happened?** — traces, logs, metrics, GenAI spans
2. **What did it cost?** — token counts x pricing = cost per call, session, service, model

qyl **observes**. It does not **control**. It sits on the side channel. If qyl goes down, apps keep working — they just lose telemetry. This is the foundational reliability contract.

### 1.1 Scope

> "Does this feature help the user **see** what their AI app did and what it cost?"
>
> If yes → it belongs in qyl. If no → it belongs somewhere else.

**In scope:**

| Domain | Why |
|--------|-----|
| OTLP telemetry ingestion | Core function — accept what apps emit |
| Trace/log/metric storage and query | Core function — answer "what happened?" |
| GenAI cost computation | First-class feature. See `cost.md`. |
| Error grouping and issue tracking | Core function — aggregate errors into issues |
| Service discovery | Derived from incoming telemetry |
| AI investigation and autofix | Standalone product (`qyl.loom`). See `src/qyl.loom/specs/loom.md`. |
| MCP tool surface | Agent-native query interface. See `mcp.md`. |

**Out of scope:**

| Feature | Why excluded |
|---------|-------------|
| LLM proxy gateway | Critical-path dependency. See `decisions/no-proxy.md`. |
| Runtime middleware (cache, PII, rate limit, content guards, failover) | Each is its own product category. qyl observes, doesn't control. |
| Agent framework | Microsoft.Extensions.AI and Microsoft.Agents.AI exist. qyl instruments agents. |
| Evaluation / LLM-as-judge | Separate product category (testing, not observability). qyl records results as traces. |
| Prompt management / versioning | Development workflow. IDEs, VCS, and LLM frameworks handle this. |
| Python / TypeScript / Go SDKs | qyl is .NET-first. Non-.NET apps use their language's OTel SDK. |
| Data export / OTLP re-export | Deferred. Implement as OTLP exporter when needed. |

### 1.2 Open Questions

| # | Question | Impact | Status |
|---|----------|--------|--------|
| 1 | Should `qyl.contracts` be a published NuGet package? | External consumers need shared types. Recommendation: YES — follows `Microsoft.Extensions.AI.Abstractions` pattern. | **Needs decision** |

---

## 2. Architecture

### 2.1 Deployment model

Single Docker image. Single process. No external dependencies.

```bash
docker run -p 5100:5100 -p 4317:4317 -p 4318:4318 ghcr.io/ancplua/qyl
```

<ports>

| Port | Protocol | Purpose |
|------|----------|---------|
| `:5100` | HTTP | Dashboard + REST API + SSE |
| `:4317` | gRPC | OTLP ingest |
| `:4318` | HTTP | OTLP ingest |

</ports>

<environment>

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_PORT` | `5100` | Dashboard + REST API |
| `QYL_GRPC_PORT` | `4317` | gRPC OTLP (`0` = disable) |
| `QYL_OTLP_PORT` | `4318` | HTTP OTLP (`0` = disable) |
| `QYL_DATA_PATH` | `qyl.duckdb` | DuckDB file path |
| `QYL_RETENTION_DAYS` | `0` (disabled) | Auto-delete old telemetry |

</environment>

### 2.2 System topology

<data-flow>

**Ingest path:**
1. Apps / Agents / Services emit OTLP telemetry (gRPC `:4317` or HTTP `:4318`)
2. `qyl.collector` receives, validates, converts proto → internal model
3. DuckDB stores spans, logs, metrics

**Query path:**
1. Dashboard / MCP / REST clients request data via HTTP `:5100`
2. `qyl.collector` queries DuckDB, returns JSON
3. SSE endpoint streams live telemetry

**Internal assemblies (single host, strict acyclic references):**

| Assembly | Role | Depends on |
|----------|------|-----------|
| `qyl.collector` | Composition root, API host, SSE, dashboard, OTLP ingest | `qyl.contracts`, DuckDB, ASP.NET Core, OTel SDK |
| `qyl.mcp` | MCP tool surface | `qyl.contracts` (compile), `qyl.collector` (HTTP at runtime) |
| `qyl.loom` | AI investigation, autofix, triage, regression | `qyl.contracts` (compile), `qyl.collector` (HTTP at runtime), M.E.AI |
| `qyl.contracts` | Interfaces, DTOs, value objects | BCL only (zero packages) |
| `qyl.instrumentation` | OTel SDK — Roslyn generators + runtime wiring | `qyl.contracts`, OTel SDK |

</data-flow>

### 2.3 Component boundaries

<tier-1 label="The Product (ships as one image)">

| Component | Responsibility |
|-----------|---------------|
| `qyl.collector` | Composition root, REST API, SSE, dashboard host, OTLP ingest, DuckDB storage |
| `qyl.contracts` | Interfaces, DTOs, value objects, query contracts |
| `qyl.instrumentation` | OTel SDK — Roslyn generators + runtime wiring |
| `qyl.dashboard` | React UI served by collector |

</tier-1>

<tier-2 label="Extensions (separate processes)">

| Component | Responsibility |
|-----------|---------------|
| `qyl.mcp` | MCP tool surface (stdio or HTTP) |
| `qyl.loom` | AI investigation, autofix, triage (standalone product) |

</tier-2>

<hard-rule>
No sibling project references. `qyl.collector` may see `qyl.contracts` and instrumentation; `qyl.mcp` communicates via HTTP only. `qyl.loom` communicates with collector via HTTP only (no ProjectReference). Loom depends on `qyl.contracts` at compile time and `qyl.collector` at runtime over HTTP.
</hard-rule>

---

## 3. Tier 1: The Product

### 3.1 qyl server

**Responsibility:** Accept OTLP telemetry, store it, serve it, compute cost. See `collector.md` for implementation detail.

**Hard constraint:** The server is the data plane. No LLM provider SDKs (OpenAI, Anthropic, etc.). References `Microsoft.Extensions.AI` abstractions (`IChatClient?`) for optional triage/autofix/review pipelines that work with heuristic fallbacks when no LLM is registered. Loom is the intelligence plane — an autonomous agent that enhances these pipelines with multi-step LLM reasoning when deployed alongside.

### 3.2 qyl dashboard

**Responsibility:** Visual interface for exploring telemetry and understanding cost.

<dashboard-views label="Core views">

| View | Purpose |
|------|---------|
| **Overview** | Health, request volume, error rate, cost summary |
| **Traces** | Trace list → trace detail → span detail |
| **GenAI** | LLM calls: model, tokens, latency, cost per call |
| **Logs** | Structured log search and exploration |
| **Cost** | Per-model, per-service, per-session cost. Trends. Budget alerts. |
| **Services** | Service map, dependencies, health |
| **Settings** | Pricing table, retention, API keys |

</dashboard-views>

<dashboard-exclusions label="Views that DO NOT belong in the dashboard">

| View | Where it goes |
|------|--------------|
| Loom investigation | `qyl.loom` (separate UI or embedded via MCP) |
| Code review results | `qyl.loom` |
| Fix runs | `qyl.loom` |
| Issue triage | `qyl.loom` |
| Alerts dashboard | Anomaly detection is a Loom capability (`AnomalyService`), not a dashboard view. Alerting is a separate product category. |
| Copilot panel | Deleted |
| Workflow runs | Deleted |
| Bot conversation | Deleted |

</dashboard-exclusions>

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

<sdk-components>

| Component | What it does |
|-----------|-------------|
| `[Traced]` attribute + generator | Compile-time span instrumentation |
| `[GenAi]` attribute + generator | GenAI semconv 1.40 span instrumentation |
| `[Db]` attribute + generator | Database call instrumentation |
| `[Counter]` attribute + generator | Metrics instrumentation |
| `InstrumentedChatClient` | `DelegatingChatClient` that emits GenAI spans |
| Collector discovery | Auto-detect qyl server on the network |
| OTLP export wiring | Configure OTel SDK to export to discovered server |

</sdk-components>

<sdk-exclusions>

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

</sdk-exclusions>

**NuGet package name:** `qyl` (single package, simple)

**Target frameworks:** `net10.0`

### 3.4 Cost engine

Cost is a first-class feature. Pure server-side computation from data qyl already stores. Zero proxy, zero middleware.

See `cost.md` for pricing schema, computation formula, aggregation endpoints, and budget alerts.

---

## 4. Tier 2: Extensions

### 4.1 qyl-mcp

MCP server for AI-native telemetry queries. Separate process. Communicates with qyl server via HTTP.

**Deployment modes:**
- `stdio` — local, for Claude Code / desktop tools
- Streamable HTTP (`/mcp`) — remote, for Anthropic directory

<mcp-tools>

| Category | Tools |
|----------|-------|
| Discovery | list-services, get-service-map, list-projects |
| Traces | search-traces, get-trace, get-span |
| Logs | search-logs |
| Metrics | list-metrics, query-metrics |
| GenAI | search-genai-spans, get-cost-summary |
| Sessions | search-sessions, get-session |
| Management | configure-retention, manage-pricing, create-api-key |

</mcp-tools>

<mcp-constraints>

| Constraint | Rationale |
|------------|-----------|
| Every tool has `readOnlyHint` / `destructiveHint` annotations | Safety annotations required by Anthropic directory |
| Every response < 25,000 tokens | Context window budget for consumers |
| All list endpoints paginated | Prevent unbounded responses |
| OAuth 2.1 + DCR for remote mode | Anthropic directory requirement |
| No direct DuckDB access — HTTP to qyl server only | Process boundary |

</mcp-constraints>

### 4.2 qyl.loom

AI investigation and automation layer. Standalone product. Containerized microservice with its own Dockerfile (`src/qyl.loom/Dockerfile`).

**Responsibility:** Given an error/anomaly, investigate root cause using AI agents with access to telemetry and artifacts.

**Dependencies:**
- `qyl.contracts` (compile-time shared types)
- `qyl.collector` (runtime HTTP via `CollectorClient`)
- Microsoft.Extensions.AI / Microsoft.Agents.AI (for agent construction)

**Deployment:**
- Own Dockerfile at `src/qyl.loom/Dockerfile`
- Runs as a Docker service alongside collector via root `docker-compose.yml`
- Communicates with collector over HTTP only (no ProjectReference, no DuckDB)

**Architecture rules:**
- `qyl.loom` depends on `qyl.contracts` at compile time and `qyl.collector` at runtime over HTTP
- No ProjectReference to collector. No DuckDB dependency.
- All LLM provider dependencies live here, not in the collector
- Shared types (`CodingAgentProvider`, `CodingAgentRunRecord`, etc.) live in `qyl.contracts/Loom/`

---

## 5. Kill List

Components deleted from qyl. Not deprecated — deleted.

<kill-list>

| Component | Status | Rationale |
|-----------|--------|-----------|
| `qyl.agents` | **Deleted** (2026-03-16) | MAF native migration. `InstrumentedChatClient` + `InstrumentedAIFunction` moved to `qyl.instrumentation`. |
| `qyl.workflows` | **Deleted** (2026-03-16) | Linear-only engine with fundamental gaps. MAF native migration. |
| `qyl.hosting` | **Deleted** | Collector IS the host. `QylApp`/`QylAppBuilder` was an unnecessary abstraction. |
| `qyl.watch` | **Deleted** | Terminal span viewer. Not core product. |
| `qyl.browser` | **Deleted** | Web Vitals for AI apps. Niche. Revisit when demand exists. |
| GenAI middleware (spec 08 sections 3.2–3.6) | **Deleted** | Cache, rate limit, PII, guards, fallback — each is its own product. qyl observes, doesn't control. |
| `Collector: Copilot/` | **Deleted** (2026-03-16) | AG-UI integration removed from collector. |
| `Collector: ClaudeCode/` | **Deleted** (2026-03-16) | Orphaned after MAF migration. |
| `Collector: CodingAgent/` | **Deleted** (2026-03-16) | Types relocated to `qyl.contracts/Loom/`. |
| `Collector: Workflow/` | **Deleted** (2026-03-16) | Workflow execution is not the server's job. |
| `Collector: BuildFailures/` | **Deleted** | Not core observability. |
| `Collector: ConsoleBridge/` | **Deleted** | Not core observability. |
| `Collector: Identity/` | **Deleted** | GitHub service moved to `qyl.loom`. |
| `Collector: Insights/` | **Deleted** | AI-generated insights moved to `qyl.loom`. |

</kill-list>

---

## 6. Naming

<naming>

| Current name | Target name | Rationale |
|-------------|------------|-----------|
| `qyl.collector` | **qyl** (the server binary) | It's the product, not a "collector" |
| `qyl.servicedefaults` | **qyl** (NuGet package) | "servicedefaults" is an Aspire-ism |
| `qyl.instrumentation` | Fold into **qyl** package | No need for two packages |
| `qyl.instrumentation.generators` | Internal to **qyl** package | Implementation detail |
| `qyl.collector.storage.generators` | Internal to **qyl** server | Implementation detail |
| `qyl.contracts` | **qyl.contracts** (unchanged) | "contracts" is the standard .NET term for shared DTOs/types |
| `qyl.dashboard` | **qyl-ui** | Shorter, clearer |

</naming>

### 6.1 Solution structure after rename

<solution-structure>

| Path | Purpose |
|------|---------|
| `src/qyl/` | The server (was `qyl.collector`) |
| `src/qyl.contracts/` | Shared types (unchanged) |
| `src/qyl.sdk/` | NuGet package (was `qyl.instrumentation`) |
| `src/qyl.sdk.generators/` | Roslyn generators (internal to SDK) |
| `src/qyl.storage.generators/` | DuckDB generators (internal to server) |
| `src/qyl-ui/` | React dashboard (was `qyl.dashboard`) |
| `src/qyl-mcp/` | MCP server (unchanged) |
| `tests/qyl.tests/` | Server tests |
| `tests/qyl.sdk.tests/` | SDK tests |
| `core/specs/` | TypeSpec definitions |
| `eng/build/` | NUKE build |
| `specs/` | Architecture specs |

</solution-structure>

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
| Runtime | .NET 10.0 LTS, C# 14, `net10.0` |
| Frontend | React 19, Vite 7, Tailwind CSS 4 |
| Storage | DuckDB (columnar, glibc required) |
| Protocol | OTel Semantic Conventions 1.40 |
| Testing | xUnit v3, Microsoft Testing Platform |
| Build | NUKE |

### 8.1 Banned APIs (unchanged)

`DateTime.Now`, `Newtonsoft.Json`, `object _lock`, `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`, `ISourceGenerator`, `SyntaxFactory.NormalizeWhitespace()`, runtime reflection, `dynamic`, `.Result`, `.Wait()`

---

## 9. Dependency Rules

<dependency-matrix>

| Source | Depends on (compile) | Depends on (runtime) |
|--------|---------------------|---------------------|
| `qyl.contracts` | BCL only | — |
| `qyl.sdk` | `qyl.contracts` | — |
| `qyl.sdk.generators` | Roslyn APIs only | — |
| `qyl` (server) | `qyl.contracts`, DuckDB, ASP.NET Core, OTel SDK, Microsoft.Extensions.AI (abstractions only) | — |
| `qyl-ui` | — | `qyl` (HTTP) |
| `qyl-mcp` | `qyl.contracts`, ModelContextProtocol SDK | `qyl` (HTTP) |
| `qyl-loom` | `qyl.contracts` | `qyl` (HTTP via CollectorClient), Microsoft.Extensions.AI, Microsoft.Agents.AI |

</dependency-matrix>

<forbidden-dependencies>

| From | To | Why |
|------|----|-----|
| `qyl` (server) | `qyl-loom` | One-way dependency: loom → qyl |
| `qyl-mcp` | `qyl` (ProjectReference) | HTTP only — separate process |
| `qyl.contracts` | any NuGet package | Shared types must be dependency-free |
| `qyl` (server) | any LLM provider SDK (OpenAI, Anthropic, etc.) | Server uses M.E.AI abstractions (`IChatClient?`) as optional deps. Provider SDKs are forbidden — those belong in Loom or external DI registration. |
| `qyl.sdk` | `qyl` (server) | SDK is client-side |

</forbidden-dependencies>

---

## 10. Migration Path

This is not a refactor. It is a rebuild with knowledge transfer.

### 10.1 What transfers (code or knowledge)

<migration-transfers>

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
| `qyl.agents/InstrumentedChatClient` | qyl SDK | Code (single file, **done** — moved to `qyl.instrumentation`) |
| `specs/08-genai-controls.md` (deleted) | `cost.md` | Knowledge (cost engine only) |
| DuckDB schema + migrations | qyl server | Code (adapt) |
| TypeSpec definitions | qyl.contracts | Code (unchanged) |
| NUKE build | eng/build | Code (adapt targets) |

</migration-transfers>

### 10.2 What does NOT transfer

Everything on the kill list (section 5). No code, no "maybe later," no commented-out stubs.

---

## 11. Acceptance Criteria

### 13.1 Product criteria

- [x] `docker run qyl` starts a working server with dashboard in < 5 seconds
- [x] `dotnet add package qyl` + `builder.AddQyl()` produces traces in the dashboard
- [x] GenAI spans show model, tokens, latency, and computed cost
- [x] Cost dashboard shows per-model, per-service, per-session aggregations
- [x] Non-.NET app emitting OTel GenAI spans appears in the same dashboard
- [x] MCP server answers "what failed in the last hour?" from qyl data
- [x] Server has no required LLM dependencies (M.E.AI abstraction interfaces present; provider SDKs forbidden)
- [x] Server process survives qyl-loom being offline

### 13.2 Architecture criteria

- [x] Collector contains <= 15 domain directories (down from 30+)
- [x] No code path in the server calls an LLM provider directly (services accept `IChatClient?` and self-disable when null)
- [x] SDK contains exactly one `DelegatingChatClient` (`InstrumentedChatClient`)
- [x] SDK contains zero middleware beyond instrumentation
- [x] qyl-mcp communicates with server via HTTP only (no ProjectReference)
- [x] qyl.contracts has zero NuGet package dependencies
- [x] No component on the kill list exists in the codebase

### 13.3 UX criteria

- [ ] Setup time (server + SDK + first trace) <= 2 minutes
- [ ] Dashboard loads in < 1 second with 100K spans in DuckDB
- [x] Cost view answers "how much did I spend today?" in one click
- [x] GenAI view answers "which model is slowest?" in one click

---

## 12. Decision Record

<decisions>

| Decision | Rationale | Alternatives rejected |
|----------|-----------|----------------------|
| No proxy | Side channel reliability contract. Provider API maintenance burden. | Helicone sidecar, custom proxy |
| No GenAI middleware (except instrumentation) | Each control is its own product category. Observability != control. | Full middleware pipeline (spec 08 v1) |
| Cost as first-class feature | Pure server-side computation from existing data. No proxy needed. | Cost as middleware, cost as separate service |
| Loom as standalone product | AI investigation has different dependencies, lifecycle, and deployment model than the observer. | Embedded agents in server (creates LLM dependency, resource contention) |
| Delete qyl.agents + qyl.workflows | MAF provides agent construction natively. Shim layers deleted. OTel wrappers moved to SDK. | Keep wrappers (unnecessary indirection) |
| Server has no required LLM dependencies | References M.E.AI abstractions for optional `IChatClient?`/`IEmbeddingGenerator?` injection. Services self-disable when no implementation is registered. Provider SDKs forbidden. | Hard-delete all M.E.AI references (breaks optional triage/autofix pipeline), Embedded provider agent in server |
| Keep `qyl.contracts` name | "contracts" is the standard .NET term for shared DTOs/types. | Rename to `qyl.protocol` (misleading) |
| Publish `qyl.contracts` as NuGet | External consumers need shared types. Follows `M.E.AI.Abstractions` pattern. | Keep internal-only (blocks ecosystem) |
| Ship bundled model pricing | Empty pricing table on first boot is dead UX. | Manual-only pricing |
| Workflow orchestration deferred | No DAG engine fits single-process model. | Build custom DAG engine, adopt Temporal |

</decisions>
