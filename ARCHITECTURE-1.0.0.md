# qyl 1.0.0 — Architecture Goal

**Status:** Target state (release gate definition)
**Scope:** Everything shipped under the `qyl` name at 1.0.0
**Test of done:** Every gate in §7 is verifiable by an agent with one command and a deterministic expected result.

**Precedence:** This document is normative. `docs/component-taxonomy.html` is a
*view* of it — on conflict, this file wins. A second source of architectural
truth would be the duplicate-contract failure the architecture itself forbids.

---

## 0. Thesis

qyl is **one schema-owned modular platform**: one dependency graph, one verification pipeline, and multiple independently shipped artifacts. MCP, CLI, instrumentation, conventions, collector, Workbench, and Runner each keep one clear responsibility without owning duplicate contracts.

The load-bearing structure is one wire and two generated loops:

> **A producer stack** that lives inside a customer's process and ends at an OTLP exporter.
> **A collector** that is a standalone binary and begins where that exporter ends.
> **Two schemas, two loops.** *Vocabulary loop:* one weaver registry generates the producer's constants **and** the collector's ingest catalog — qyl cannot emit telemetry its own collector does not recognize. *Contract loop:* one TypeSpec repo (`qyl-api-schema`) generates the collector's API surface **and** every first-party client of it, including the MCP tool shapes — no client can hold a shadow contract.

Every other component is a client or projection of the collector API — never a second owner of its contract. **One graph, one truth, many artifacts.**

```text
customer app process                          qyl collector process
┌────────────────────────────────┐            ┌────────────────────────────────┐
│ Qyl.Telemetry.SemanticConv.    │            │ Qyl.Collector (ingest 4317/18) │
│ Qyl.Telemetry                  │   OTLP     │ Qyl.Collector.Storage (DuckDB) │
│ Qyl.Telemetry.AutoInstr.       │ ─────────► │ Qyl.Collector.Auth             │
│ Qyl.Telemetry.Hosting AddQyl() │  network   │ Collector API + health surface │
│   └── OTLP exporter ───────────┼──────────► │ CollectorSemanticAttribute-    │
└────────────────────────────────┘            │   Catalog.g.cs  ◄── same YAML  │
              ▲                               └───────┬────────────────────────┘
              │                                       │ collector API
   loop 1: weaver registry (YAML) ────────────────────┤ ◄── loop 2: qyl-api-schema
   single source of vocabulary                        │     (TypeSpec) → generated
                                                      │     server surface + clients
        ┌──────────────┬──────────────────────────────┴┐
        ▼              ▼                               ▼
     Qyl.Cli       qyl.dashboard                qyl.mcp/server ◄─── MCP clients
    (qyl up)       (product UI)                 (closed-world       (closed world)
        │                                        MCP projection)
  Qyl.Run.Workload                              qyl.mcp/workbench ──► arbitrary external
  (Runner: local process                        (open-world MCP       MCP servers
   supervisor under qyl up)                      client, :18888)      (runtime-validated)
```

The wire is the architecture boundary; no package crosses it. The only artifacts shared across boundaries are *generated code from a single source* — the vocabulary (loop 1) and the contract (loop 2) — never runtime dependencies.

---

## 1. Process topology

Two process roles exist. A third is derived.

**Producer (customer app).** References exactly one package (`Qyl.Telemetry.Hosting`), calls `builder.AddQyl()`, and thereafter produces spans, metrics, and logs that leave via OTLP. The producer stack never stores, never queries, never validates ingest, and never knows the collector exists beyond an endpoint URI.

**Collector (qyl binary).** NativeAOT-published service. Listens on OTLP gRPC/HTTP, validates ingest against the generated catalog, persists to DuckDB, serves the query/health/API surface. It never references any `Qyl.Telemetry.*` package for its *product* function.

**Collector-as-producer (derived).** The collector process also instruments *itself* — it consumes the producer stack for self-telemetry only. This is dogfooding, not a layering violation, under one hard invariant:

> **Self-export invariant:** the collector's own OTLP exporter must never target its own ingest ports. `CollectorSelfExportGuard.ThrowIfSelfExporting` enforces this at startup, fail-closed. Removing or bypassing the guard is a release blocker.

**Clients and projections (product plane).** `Qyl.Cli` (`qyl up`) orchestrates the local stack — collector on `:5100`, diagnostics on `:5200`, OTLP ingest on `:4318` — and is a *client* of the collector API, never the API itself; `Qyl.Run.Workload` (Runner) is the in-proc supervisor of those local processes. `qyl.dashboard` presents product telemetry over HTTP. All of them reach the collector exclusively through its API using **generated contract clients** (loop 2); process spawning by CLI/Runner is process-level, not a package edge. The kubectl principle: CLI talks to the API, owns none of it.

**MCP plane (two nodes, one asymmetry).** `qyl.mcp/server` is the *closed-world* MCP server (Node service `qyl-mcp`, Railway, `mcp.qyl.at`): it projects the known qyl model as MCP tools over stored telemetry — a projection of the collector API, with tool *shapes* generated from the contract and tool *curation* authored (§4, loop 2). `qyl.mcp/workbench` is the *open-world* MCP client runtime (local loopback `:18888`): it connects to, inspects, and tests arbitrary external MCP servers it didn't write, so it validates schemas at runtime **by design** and is deliberately outside both loops. `qyl.mcp/dashboard` is the browser Workbench UI (Vite bundle + MCP App served by the server) — its subject is MCP, its protocol is HTTP; a browser cannot be an MCP stdio client.

The self-telemetry rule generalizes: any first-party process (CLI, Runner, MCP server host, dashboards' backends) may consume `Qyl.Telemetry.Hosting` for its **own** telemetry, under the same fail-closed guard discipline as the collector.

---

## 2. Package family (final naming)

`Qyl.Sdk` is retired. "SDK" promised a foundational programming model the package never was; what it actually did was composition. The name goes; the one-liner (`builder.AddQyl()`) stays.

### Producer side

| Package | Responsibility | Must NOT contain |
|---|---|---|
| `Qyl.Telemetry.SemanticConventions` | Generated stable vocabulary from pinned registries | Any reference to `Activity`, `Meter`, DI, OTLP |
| `Qyl.Telemetry.SemanticConventions.Incubating` | Generated unstable/development vocabulary, GenAI payload schemas | Same |
| `Qyl.Telemetry` | qyl telemetry primitives: `ActivitySource`/`Meter` ownership, names, shared options, session identifiers, explicit `StartOperation`-style API | Exporters, discovery, interception, DI where avoidable |
| `Qyl.Telemetry.AutoInstrumentation` | Automatic capture: interceptors, DiagnosticSource listeners, framework hooks, generated interception code | Exporters, collector discovery, resource config, the consumer's OTel pipeline |
| `Qyl.Telemetry.AutoInstrumentation.<Integration>` | Per-integration capture (EntityFrameworkCore, SqlClient, …) | Same as parent |
| `Qyl.Telemetry.Hosting` | Composition root: `AddQyl()`, source/meter registration, resource identity, processors, OTLP export config, collector discovery | Telemetry production of its own; storage; anything server-side |

### Collector side

| Package | Responsibility |
|---|---|
| `Qyl.Collector` | OTLP ingest, ports, pipeline, `CollectorSemanticAttributeCatalog.g.cs` |
| `Qyl.Collector.Storage` | DuckDB persistence |
| `Qyl.Collector.Auth` | API-key / auth surface |
| `Qyl.Collector.Hosting` | Process bootstrap, Kestrel config, guards, service defaults |

### Platform components (clients, projections, runtime)

| Component | Responsibility | Ships as |
|---|---|---|
| `Qyl.Api.Contracts` (+ generated TS artifacts) | The two generated faces of the one API contract (`qyl-api-schema`, TypeSpec): .NET for collector + CLI, TypeScript (types, JSON Schemas, client) for MCP server + dashboards | Generated packages — never hand-edited |
| `Qyl.Cli` | Local stack orchestration (`qyl up`); client of the collector API | NuGet global tool `qyl` (`qyl/packages/Qyl.Cli`) |
| `Qyl.Run.Workload` | Runner: supervises local runtime processes under `qyl up` | .NET process (`qyl/packages/Qyl.Run.Workload`) |
| `qyl.dashboard` | Product telemetry UI | Web bundle (`qyl/services/qyl.dashboard`) |
| `qyl.mcp/server` | Closed-world MCP projection of the qyl model | Node service `qyl-mcp` · Railway · `mcp.qyl.at` (npm `qyl-mcp-server`) |
| `qyl.mcp/workbench` | Open-world MCP client runtime for arbitrary external servers | Node loopback process · `:18888` |
| `qyl.mcp/dashboard` | Workbench UI | Vite bundle + MCP App, served by the server |

### Dependency edges (exhaustive — anything not listed is forbidden)

```text
Qyl.Telemetry
└── Qyl.Telemetry.SemanticConventions

Qyl.Telemetry.AutoInstrumentation
├── Qyl.Telemetry
├── Qyl.Telemetry.SemanticConventions
└── Qyl.Telemetry.SemanticConventions.Incubating

Qyl.Telemetry.Hosting
├── Qyl.Telemetry
├── Qyl.Telemetry.AutoInstrumentation
├── OpenTelemetry.Extensions.Hosting
└── OpenTelemetry.Exporter.OpenTelemetryProtocol

Qyl.Collector.*        (product function)
├── Qyl.Telemetry.SemanticConventions        ← catalog generation input only
└── (no other Qyl.Telemetry.* reference)

Qyl.Collector host process (self-telemetry only)
└── Qyl.Telemetry.Hosting                    ← permitted, guarded by SelfExportGuard

Qyl.Cli
├── Qyl.Api.Contracts                        ← generated API client, only path to the collector
├── Qyl.Telemetry.Hosting                    ← own self-telemetry only
└── (zero Qyl.Collector.* references — collector is spawned as a process, reached via API)

qyl.mcp/server  (Node — rule-level, verifier-enforced)
├── generated TS contract artifacts          ← all tool/request/response shapes import from here
└── (zero hand-declared API shapes; curation manifest references generated shapes only)

qyl.mcp/workbench
└── (no qyl contract dependency — open world, runtime schema validation by design)
```

**`InternalsVisibleTo` count across the producer family: 0.** The current `<InternalsVisibleTo Include="Qyl.Sdk" />` exists because foundational primitives (source names, meter names, options) live inside AutoInstrumentation. Moving them into `Qyl.Telemetry` dissolves the need. Any surviving IVT at 1.0.0 marks a responsibility that lives in the wrong package.

---

## 3. Consumer contract

Default consumer — unchanged experience, honest package name:

```xml
<PackageReference Include="Qyl.Telemetry.Hosting" />
```

```csharp
builder.AddQyl();
```

Advanced consumer, manual instrumentation only (no auto-capture, no pipeline opinion):

```xml
<PackageReference Include="Qyl.Telemetry" />
```

Advanced consumer, own OTel pipeline (auto-capture without qyl's export composition): reference `Qyl.Telemetry.AutoInstrumentation` and register sources/meters explicitly. Hosting is convenience, never a requirement — a design property that gate G6 makes testable.

---

## 4. The two closed loops

### Loop 1 — vocabulary (weaver registry)

The telemetry-defining property, beyond package hygiene: **the collector validates ingest against the same registry from which its own instrumentation is generated.**

Single source: the weaver registry (pinned upstream OpenTelemetry YAML + qyl-owned `qyl.*` entries, each with stability, brief, unit, requirement level — first-class next to upstream vocabulary).

Generated from it, in one command:

1. Stable + incubating constant classes (producer side)
2. Metric descriptors and meter factories
3. `CollectorSemanticAttributeCatalog.g.cs` (collector side)
4. Generated doc-comments — making `qyl.collector.*` etc. documented public surface customers can read, identically to upstream semconv

Consequences that are product claims, not implementation details:

- It is structurally impossible for qyl to emit a metric its own catalog doesn't know. Semconv compliance — including for qyl's own vocabulary — is compile-time-proven.
- Registry drift (upstream or `qyl.*`) surfaces as a build/snapshot failure, never as silently stale strings on the wire.
- Hand-maintained vocabulary files (`GenAiConstants.cs`, `SemConv.cs`, `HttpTelemetryNames.cs`) are deleted or reduced to generated constants.

### Loop 2 — API contract (`qyl-api-schema`, TypeSpec)

Same pattern, second schema: **every first-party consumer of the collector API compiles against artifacts generated from the one TypeSpec source** — `Qyl.Api.Contracts` (.NET: collector serves it, `Qyl.Cli` calls it) and the generated TS artifacts (types, JSON Schemas, client: `qyl.mcp/server` and the dashboards).

For the MCP server the rule has a deliberate two-layer shape, because MCP tools are not REST endpoints:

- **Shapes are generated.** Every tool `inputSchema`/output shape, every request/response type, every path imports from the generated contract artifacts. A hand-declared shape anywhere in the server is a verifier failure.
- **Curation is authored.** Which tools exist, how they're named for an agent, their descriptions, examples, pagination/summarization behavior, and which contract operations they compose — that is hand-written product design, referencing generated shapes only. **1:1 endpoint→tool mirroring is explicitly a non-goal**: a mechanical projection of the API would make a worse MCP server, not a more correct one.
- **Revision handshake, fail-closed.** The collector advertises its contract revision on its meta/health surface; `qyl.mcp/server` compares it to the revision baked into its generated artifacts at startup and throws on mismatch — the SelfExportGuard pattern applied to the contract axis. Lockstep deploys are the honest cost of one contract; for a solo-operated Railway pair, that cost is near zero.

The Workbench is *intentionally* outside this loop: an open-world client's entire job is handling servers with no shared static contract. Binding it to qyl's schema would be a category error, not a hygiene win.

Consequence, mirroring loop 1: it is structurally impossible for the MCP server (or the CLI) to describe an operation the collector doesn't serve, or to drift when the contract changes — the shadow-contract failure mode is deleted, not policed.

---

## 5. Enforcement over convention

Every rule in this document that can be owned by the compiler is owned by the compiler, so the corresponding instruction can be deleted from CLAUDE.md/AGENTS.md:

| Rule | Enforcer | Failure mode |
|---|---|---|
| No hardcoded telemetry strings in hand code | `QYL0200` (+`QYL0201`) reading allowed names **from the generated catalog**, not from hardcoded lists | Build error |
| No forbidden package edges | Dependency test asserting the §2 edge list against actual `PackageReference`s | Test failure |
| Collector never exports to itself | `CollectorSelfExportGuard` | Startup exception |
| Health surface always wired | `CollectorHealthGuard.ThrowIfHealthSurfaceUnwired` | Startup exception |
| Registry drift visible | ByteIdentity snapshots + new snapshot test for the `qyl.*` projection | Snapshot diff |
| NativeAOT holds everywhere | AOT publish gate in CI for collector and conformance app | Publish failure |
| No hand-declared API shapes in first-party clients | Generated contract artifacts + verifier grep gate (no shape literals outside `*.gen.*`) + tool-manifest snapshot pinned to the TypeSpec revision | Verifier / snapshot failure |
| MCP server ↔ collector contract revision match | Startup handshake against the collector's advertised revision | Startup exception |
| Clients never link collector packages | Dependency test (§2 edge list, incl. client rules) | Test failure |

---

## 6. Non-goals for 1.0.0

- No backcompat shims for `Qyl.Sdk` consumers (solo-dev no-backcompat corollary; hard rename, single release note).
- No OpenAPI/contract generation from the collector — the API contract lives in the external TypeSpec repo and flows in via `Qyl.Api.Contracts` and the generated TS artifacts. Corollary: no 1:1 endpoint→tool mirroring in the MCP server; shapes are generated, curation is authored (§4, loop 2).
- No contract coupling for the Workbench — the open-world client validates external servers at runtime by design.
- No runtime registry loading, no reflection-based anything, no plugin model. If a capability can't be generated or compiled in, it waits.
- No multi-collector federation, no clustering. One binary, one DuckDB.

### 6.1 Versioning reality the rename must respect

"Hard rename, no backcompat shim" is right about *people* and wrong about *the
registry*. The old IDs are already published as stable on nuget.org —
`Qyl.OpenTelemetry.SemanticConventions 4.0.0`,
`Qyl.OpenTelemetry.AutoInstrumentation 8.5.0`, `Qyl.Sdk 8.5.0` — with real
download counts. NuGet IDs are permanent: they can be unlisted, never deleted.
The `qyl` CLI versions honestly (`0.1.0-beta.N`); the libraries do not.

The migration therefore:

- ships the new `Qyl.Telemetry.*` family at **`1.0.0-beta.N`** until launch, so
  the version number stops promising what the taxonomy says is unsettled;
- **unlists** the old IDs at launch rather than shimming — a compat shim would
  create exactly the second contract owner the boundary law forbids;
- goes `1.0.0` only at launch, at which point every item in this document is
  frozen and changes need backwards compatibility, a shim, or a PR.

---

## 7. Release gates (all agent-verifiable)

1.0.0 ships when every gate below passes. Each is one command, deterministic, no human judgment required.

**G1 — Vocabulary completeness.**
`grep -rn '"qyl\.' --include='*.cs'` over the workspace → **0 hits** outside `*.g.cs` and the registry input itself.

> **Open defect (measured 2026-07-26, 78 hits).** As written this gate can never pass: the
> pattern also matches package IDs (`qyl.linux-x64`), directory names (`qyl.collector`,
> `qyl.dashboard`), and executable names (`qyl.exe`) that are not telemetry vocabulary. The
> gate's intent is *no hardcoded telemetry attribute names*. Before 1.0.0 the command must be
> narrowed to that intent — e.g. restricted to `SetTag`/`AddTag`/meter-name argument positions,
> or to strings matching a telemetry-namespace pattern — or G1 is unfalsifiable and G4's
> analyzer is doing the real work alone.

**G2 — One command, one diff.**
Edit one registry YAML line → run `./generate.sh` (Nuke target) → `git status` shows only intended generated changes. The nuke-and-regen proof: the full codebase effect of a vocabulary change is one command, deterministic.

**G3 — Closed loop.**
Emitting constants and `CollectorSemanticAttributeCatalog.g.cs` provably derive from the same registry revision (asserted by a test comparing generation provenance, not by convention).

**G4 — Analyzer enforcement live.**
Introducing a hardcoded telemetry string in hand code fails the build via `QYL0200` sourced from the generated catalog. The corresponding prose rule is deleted from the instructions.

**G5 — Boundary isolation, producer.**
The conformance app (`AddQyl()` + inbound span assertion on `Qyl.Telemetry.AutoInstrumentation` source + loopback outbound call) passes **with no collector running** and with zero `Qyl.Collector.*` references.

**G6 — Boundary isolation, collector.**
The collector's ingest/storage/query tests pass driven by a **plain OTLP client**, with zero producer-family references in the test project. If G5 or G6 ever requires the other side's packages, the boundary has leaked — release blocker.

**G7 — Dependency edges.**
Automated test asserts the actual package graph equals the §2 edge list exactly, and `InternalsVisibleTo` count in the producer family is 0.

**G8 — Guards fail closed.**
Startup tests: self-export config → throws; unwired health surface → throws.

**G9 — Standard gates green, unchanged.**
`-warnaserror` build, full test run, NativeAOT publish of collector and conformance app, ByteIdentity snapshots in the semconv repo, plus the new `qyl.*` projection snapshot.

**G10 — Contract loop closed (MCP + CLI).**
(a) Verifier: zero hand-declared request/response/tool-input shapes in `qyl.mcp/server` and `Qyl.Cli` outside generated files — every shape imports from the generated contract artifacts. (b) Tool-manifest snapshot pinned to the TypeSpec revision: a contract change without regeneration is a snapshot failure. (c) Startup handshake test: revision mismatch between `qyl.mcp/server` and the collector's advertised contract revision → throws, fail-closed. Curation (tool names, descriptions, composition) remains authored and is explicitly *not* asserted 1:1 against endpoints.

**G11 — Client isolation.**
`Qyl.Cli`, `qyl.mcp/*`, and the dashboards carry **zero** `Qyl.Collector.*` package references; the collector is reachable only via its API. (Producer self-telemetry via `Qyl.Telemetry.Hosting` remains permitted per §1.) Extends the G7 edge assertion to the client ring.

**Scoreboard at release:** hardcoded-string count 0 · regeneration commands 1 (vocabulary) + 1 (contract) · analyzers/verifiers enforcing them ≥2 · IVT count 0 · cross-boundary package references 0 · hand-declared API shapes in first-party clients 0.

---

## 8. One-line definitions (canonical)

> `Qyl.Telemetry.SemanticConventions` tells qyl **what** vocabulary to emit.
> `Qyl.Telemetry` defines qyl's telemetry **primitives** and explicit API.
> `Qyl.Telemetry.AutoInstrumentation` implements **how** telemetry is captured automatically.
> `Qyl.Telemetry.Hosting` composes the producer pipeline inside an application and **ends at the OTLP exporter**.
> `Qyl.Collector` is a standalone service that **begins where the exporter ends**: it receives, validates against the shared registry, stores, and serves telemetry.
> `Qyl.Api.Contracts` (+ the generated TS artifacts) are the **two generated faces of the one API contract** — never hand-edited, never duplicated.
> `Qyl.Cli` is a **client** of the collector API and the orchestrator of the local stack — never the API itself.
> `Qyl.Run.Workload` is the **Runner**: it supervises the local runtime processes under `qyl up`.
> `qyl.mcp/server` **projects** the known qyl model as MCP tools — shapes generated from the contract, curation authored, never a second contract source.
> `qyl.mcp/workbench` is the **open-world** MCP client for arbitrary external servers — runtime validation by design, deliberately outside both loops.
> `qyl.mcp/dashboard` presents the Workbench in the browser — its **subject** is MCP, its **protocol** is HTTP.

---

## 9. Lifecycle of this document

While the rename is outstanding this file is `ARCHITECTURE-1.0.0.md` and is
framed as target state. At 1.0.0 it becomes `ARCHITECTURE.md`: the `-1.0.0`
suffix and "Target state" framing drop, §7 changes from release gates to
standing CI assertions, §2's retirement note for `Qyl.Sdk` goes, and the
document describes reality instead of intent.

`docs/component-taxonomy.html` follows the same lifecycle. Its §2 naming ledger
(target ↔ today) is migration scaffolding: once the rename lands it is dead
information and gets deleted, reduced to the two genuinely open items (dashboard
consolidation, guard invariants). The matrix itself stays.
