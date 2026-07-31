# qyl 1.0.0 — Architecture Goal

**Status:** Target state (release gate definition)
**Scope:** Everything shipped under the `qyl` name at 1.0.0
**Test of done:** Every gate in §7 is verifiable by an agent with one command and a deterministic expected result.

**Precedence:** This document is normative. `docs/component-taxonomy.html` is a
*view* of it — on conflict, this file wins. A second source of architectural
truth would be the duplicate-contract failure the architecture itself forbids.

**Reality protocol:** This document states the *target*. During migration, code
that contradicts it is expected, not alarming. On finding a contradiction:
verify it with one command, check the naming ledger — most gaps are already
registered as fold/rename rows — add a genuinely new gap to the ledger in the
same commit as the evidence, close it if it's in the active work order, and
continue. A contradiction is grounds to record and proceed; it is never grounds
to stall, to re-audit the workspace, or to silently amend this document.

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

**Collector (qyl binary).** NativeAOT-published service. Listens on OTLP gRPC/HTTP, validates ingest against the generated catalog, persists to DuckDB, serves the query/health/API surface. It never references any `Qyl.Telemetry.*` package for its *product* function. Signal scope: traces and logs have full ingest→storage→query→dashboard verticals; metrics are accepted at the standard OTLP endpoints but not stored (counted, discarded, acknowledged `partial_success`); other OTLP signals have no endpoint.

**Collector-as-producer (derived).** The collector process also instruments *itself* — it consumes the producer stack for self-telemetry only, through the published hosting package like any other application, never through a private copy of the composition logic. This is dogfooding, not a layering violation, under one hard invariant:

> **Self-export invariant:** the collector's own OTLP exporter must never target its own ingest ports, and "no endpoint" must mean "do not export" — the OTLP exporter's silent `localhost` default is this process's own ingest port, so the invariant needs independent layers, not one check: `CollectorSelfExportGuard.ThrowIfSelfExporting` (explicit endpoint), discovery disabled in the collector's own composition, and a required-endpoint flag that closes the default-fallback case. All fail closed at startup. Weakening or bypassing any layer is a release blocker; placement and per-layer reasoning live in the repository's `AGENTS.md`.

> **Why this is stated as an invariant (fork retired 2026-07-26, re-measured 2026-07-28).** The collector once composed self-telemetry through an internal copy of the composition logic — `qyl/internal/qyl.instrumentation` — which drifted against the published stack until the fold retired it. That project now consumes `AddQyl()` like any other application: no ActivitySource inventory beside the generated one, no second discovery implementation, and no agent-framework package anywhere in the collector's dependency closure. G7 is what keeps it retired — its edge assertion covers every project, `internal/` included, so a parallel producer implementation is a forbidden edge whatever directory it hides in. The prohibition is independent of the fork that prompted it: re-deriving OTel wiring, an ActivitySource inventory, or collector discovery inside the collector is forbidden whether or not one exists today, and G6 keeps that testable by requiring the collector's own tests to pass driven by a plain OTLP client with zero producer-family references. Dated history for this row lives in the ledger's fold row, not here; what remains on it is naming (§9), not architecture.

**Clients and projections (product plane).** `Qyl.Cli` (`qyl up`) orchestrates the local stack — collector on `:5100`, diagnostics on `:5200`, OTLP ingest on `:4318` — and is a *client* of the collector API, never the API itself; `Qyl.Run.Workload` (Runner) is the in-proc supervisor of those local processes. `qyl.dashboard` presents product telemetry over HTTP. All of them reach the collector exclusively through its API using **generated contract clients** (loop 2); process spawning by CLI/Runner is process-level, not a package edge. The kubectl principle: CLI talks to the API, owns none of it.

**MCP plane (two nodes, one asymmetry).** `qyl.mcp/server` is the *closed-world* MCP server (Bun service `qyl-mcp`, Railway, `mcp.qyl.at`): it projects the known qyl model as MCP tools over stored telemetry — a projection of the collector API, with tool *shapes* generated from the contract and tool *curation* authored (§4, loop 2). `qyl.mcp/workbench` is the *open-world* MCP client runtime (local loopback `:18888`): it connects to, inspects, and tests arbitrary external MCP servers it didn't write, so it validates schemas at runtime **by design** and is deliberately outside both loops. `qyl.mcp/dashboard` is the browser Workbench UI (Vite bundle + MCP App served by the server) — its subject is MCP, its protocol is HTTP; a browser cannot be an MCP stdio client.

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
| `qyl.mcp/server` | Closed-world MCP projection of the qyl model | Bun service `qyl-mcp` · Railway · `mcp.qyl.at` (npm `qyl-mcp-server`) |
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
├── (no other Qyl.Telemetry.* reference)
└── (no agent-framework or AI-runtime packages — a telemetry sink does not
     embed an agent runtime; self-telemetry never justifies one)

Qyl.Collector host process (self-telemetry only)
├── Qyl.Telemetry.Hosting                    ← permitted, guarded by SelfExportGuard
└── (never a private in-repo copy of the producer composition — the fork the
     ledger retires is a forbidden edge, not a variant)

Qyl.Cli
├── Qyl.Api.Contracts                        ← generated API client, only path to the collector
├── Qyl.Telemetry.Hosting                    ← own self-telemetry only
└── (zero Qyl.Collector.* references — collector is spawned as a process, reached via API)

qyl.mcp/server  (Bun — rule-level, verifier-enforced)
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

### Workflow state — journal authority and disposable projections

Workflow persistence has two owners and no third copy of truth. `qyl-api-schema`
owns every public HTTP, SSE, and curated MCP workflow shape; its generated C# and
TypeScript artifacts preserve branded identifiers, dedicated opaque cursors, closed
projection-status variants, and structured deleted, cursor, unavailable, and corrupt
errors. The collector owns the private persistence implementation. Its append-only
DuckDB journal is the sole authoritative record of workflow history; run summaries,
graphs, nodes, edges, statistics, manifests, repair state, and checkpoint files are
derived and may be discarded and rebuilt. Durable deletion is a tombstone that blocks
new events and stale publication without erasing journal history during ordinary
retention.

DuckDB.NET 1.5.5 is the storage floor and its APIs divide by semantics. Generated
`DuckDBAppender.AppendRow<TState>` writers with reusable rows and static callbacks own
eligible append-only ingestion; native `byte[]` mapping owns BLOB columns. Journal
insertion itself remains typed, parameterized, transactional SQL wherever sequence
allocation, idempotency, `ON CONFLICT`, affected-row counts, CAS, or `RETURNING` are
required. Generated Arrow readers use streaming mode and asynchronous record batches
for reconstruction and other bulk internal scans, dispose each batch at the generated
ownership boundary, propagate cancellation, and convert directly into private
projector state. Small point reads remain typed ADO.NET. Arrow and DuckDB storage types
never cross the public contract boundary.

Each run generation has at most one committed manifest referencing one immutable,
content-addressed checkpoint containing its complete derived graph. A checkpoint is
trusted only when its generation, included journal position, canonical journal/input
hash, projector semantic fingerprint, configuration fingerprint, format version,
byte length, and SHA-256 content address all match. Reads continue incrementally from
that committed position; they do not replay the complete journal after a valid
checkpoint exists. The bounded projection runtime coalesces demand per generation,
distinguishes rotation from deletion, transfers waiters to a live successor, preserves
cancellation ownership, and classifies DuckDB failures by the exhaustive 1.5.5
`DuckDBErrorType` surface. Retryable failures receive bounded storage-level retries;
constraint, schema, corruption, and programmer failures never become caller retry
loops.

Checkpoint replacement is write–flush-to-disk–close–validate–CAS-publish. The previous
manifest and file remain active until that CAS succeeds; a loser reloads the winner and
cannot overwrite it. A single hosted reconciliation owner validates manifests,
schedules rebuilds for missing, corrupt, stale, or incompatible state, and removes
temporary or orphaned files only after the safety interval. It never edits journal
history to make a projection valid. Structured owned logs cover journal commit counts
and latency, projection queue/coalescing/lifecycle and processed positions, full versus
incremental work, checkpoint bytes and validation reasons, CAS outcomes, repairs,
orphan cleanup, typed DuckDB classifications, and Arrow batch/row counts without
workflow payloads or secrets.

Checkpoint filesystem containment is platform-owned. Linux and macOS use pinned
directory handles, no-follow operations, and the native atomic `openat` create. Windows
uses rooted component validation with reparse-point rejection plus the platform's
atomic create-new and no-overwrite move operations. All six published qyl RIDs keep the
same journal/checkpoint behavior; a platform may not silently fall back to memory-only
derived state.

The private DuckDB schema and access paths are generated from one metadata model:
canonical DDL, stable column order and types, authoritative and disposable SHA-256
schema identities, appender writers, Arrow mappings, and verifier metadata. The active
hashes live in `qyl_schema_meta`. Empty databases are created directly; disposable
derived tables are dropped and recreated on mismatch. A mismatch touching non-empty
authoritative run or journal tables fails closed and requires an explicit,
operator-visible reset or a separately proven journal-preserving replacement. There is
no ALTER/backfill compatibility-migration framework, persisted graph table, replay-on-
read implementation, hand-written public workflow DTO, caller retry patch, or manual
hot-path storage adapter to preserve.

#### Workflow-storage acceptance evidence

The remake was measured on 2026-07-31 on the same Apple M4 arm64 host with the
same deterministic `Journal_pages_bound_large_histories` workload (2,000 events in
four 500-event batches). Commit `61decc36` is the parameterized-per-row baseline;
the accepted generated-appender implementation is the comparison. These are
observed release measurements, not permanent latency thresholds.

| Measure | Baseline | Generated appender |
| --- | ---: | ---: |
| Test duration | 3.338 s | 1.152 s |
| Throughput | 599 events/s | 1,736 events/s |
| Runtime-reported managed allocation | 8.99 MB | 7.69 MB |
| Peak resident set | 494.9 MB | 177.5 MB |

The full 2,000-event checkpoint probe produced a 1,168-byte content-addressed
checkpoint and rebuilt it from the journal after deleting the committed file in
58.8 ms. The final collector suite completed 195/195 tests in 50.4 seconds; the
earlier multi-hour run was a stranded test process, not expected backend duration.
Generated-source tests additionally assert the reusable `AppendRow<TState>` row,
static callback, direct `byte[]`/BLOB mapping, streaming Arrow mode, and asynchronous
batch ownership.

---

## 5. Enforcement over convention

Every rule in this document that can be owned by the compiler is owned by the compiler, so the corresponding instruction can be deleted from CLAUDE.md/AGENTS.md. Verification artifacts — verifiers, snapshots, PublicAPI baselines, ABI anchors — are themselves contracts: they change through their owning bump rules and regeneration, never by loosening them so a change passes.

| Rule | Enforcer | Failure mode |
|---|---|---|
| No hardcoded telemetry strings in hand code | `QYL0200` (+`QYL0201`) reading allowed names **from the generated catalog**, not from hardcoded lists | Build error |
| No forbidden package edges | Dependency test asserting the §2 edge list against actual `PackageReference`s | Test failure |
| Collector never exports to itself | Layered, fail-closed: `CollectorSelfExportGuard` + discovery off in own composition + required-endpoint flag ("no endpoint" = "do not export") | Startup exception |
| Health surface always wired | `CollectorHealthGuard.ThrowIfHealthSurfaceUnwired` | Startup exception |
| Registry drift visible | ByteIdentity snapshots + new snapshot test for the `qyl.*` projection | Snapshot diff |
| NativeAOT holds everywhere | AOT publish gate in CI for collector and conformance app | Publish failure |
| No hand-declared API shapes in first-party clients | Generated contract artifacts + verifier grep gate (no shape literals outside `*.gen.*`) + tool-manifest snapshot pinned to the TypeSpec revision | Verifier / snapshot failure |
| MCP server ↔ collector contract revision match | Startup handshake against the collector's advertised revision | Startup exception |
| Clients never link collector packages | Dependency test (§2 edge list, incl. client rules) | Test failure |
| Publishing requires a human act | Publish workflows trigger on version tags only — a push to `main` builds and verifies, never publishes | No publish without a tag |

---

## 6. Non-goals for 1.0.0

- No backcompat shims for `Qyl.Sdk` consumers (solo-dev no-backcompat corollary; hard rename, single release note).
- No OpenAPI/contract generation from the collector — the API contract lives in the external TypeSpec repo and flows in via `Qyl.Api.Contracts` and the generated TS artifacts. Corollary: no 1:1 endpoint→tool mirroring in the MCP server; shapes are generated, curation is authored (§4, loop 2).
- No contract coupling for the Workbench — the open-world client validates external servers at runtime by design.
- No runtime registry loading, no reflection-based anything, no plugin model. If a capability can't be generated or compiled in, it waits.
- No multi-collector federation, no clustering. One binary, one DuckDB.
- No metrics storage at 1.0.0 — metrics are accepted on the wire, counted, discarded with `partial_success`. The vertical exists when it exists; half-storing a signal is worse than honestly declining it.

### 6.1 Versioning reality the rename must respect

"Hard rename, no backcompat shim" is right about *people* and wrong about *the
registry*. The old IDs are already published as stable on nuget.org —
`Qyl.OpenTelemetry.SemanticConventions 4.0.0`,
`Qyl.OpenTelemetry.AutoInstrumentation 8.5.0`, `Qyl.Sdk 8.5.0` — with real
download counts. NuGet IDs are permanent: they can be unlisted, never deleted.
The `qyl` CLI versions honestly (`0.1.0-beta.N`); the libraries do not.

The migration's versioning rule, as amended by Alex at the launch review
(in chat, 2026-07-27, replacing the skipped `1.0.0-beta.N` staging band and
the earlier "every repo releases 1.0.0" phrasing that the registries
disproved):

> Launch is the event, not the number. Identity is per-package lineage: the
> product surfaces ship 1.0.0; the producer family carries its ABI lineage
> (package major == QylGeneratedCodeAbi major, enforced by
> verify-version-sync); the API contract carries its own (4.x,
> revision-hash-pinned). Superseded IDs are unlisted after indexing, never
> shimmed. The next breaking bundle (#12+#13) ships as one major.

Consequences that stand from the earlier text:

- no compat shims — a shim would create exactly the second contract owner the
  boundary law forbids; for the package IDs that survive
  (`Qyl.Api.Contracts`, `@ancplua/qyl-api-schema`), the superseded *versions*
  are unlisted/deprecated after the launch versions index, so the launch
  version is the visible latest;
- at launch every item in this document is frozen and changes need backwards
  compatibility, a shim, or a PR.

### 6.2 ABI reality the rename must respect

The generated interceptor namespace is not internal naming: consumers compile
generated code into their own assemblies under
`Qyl.OpenTelemetry.AutoInstrumentation.GeneratedCode`, four verifiers pin the
exact string, and `QylGeneratedCodeAbi.V8` anchors it — the owning repo's
contract is "do not rename or re-derive it" and "V<major> bump on a breaking
ABI change". The rename therefore decomposes into two slices with different
rules:

- **ABI-free slice.** `Qyl.Sdk` → `Qyl.Telemetry.Hosting` changes PackageId
  and AssemblyName only: the namespace stays `Qyl` and `builder.AddQyl()` is
  unchanged for every consumer — the ledger's promise ("composition job moves,
  consumer contract unchanged") holds literally. This slice can land first and
  alone.
- **ABI-carrying slices.** The semconv generated namespace (emitted by
  `emit_*.py`) and the AutoInstrumentation `GeneratedCode` namespace are
  consumer-compiled ABI. Their rename ships as the **birth ABI of the new
  package IDs**: a new ID is a new contract, so this is not a break to a
  published package — `QylGeneratedCodeAbi` bumps V8 → V9 per the owning
  repo's rule, the old IDs stay frozen at V8 until unlisted at launch, and
  ByteIdentity snapshots, PublicAPI baselines, and the pinned verifier tokens
  regenerate in the same commit as the namespace change. G2's discipline
  applies to ABI artifacts too: one change, one regeneration, one diff.
  (Published reality, 2026-07-28: the family shipped 9.0.x with the anchor at
  `QylGeneratedCodeAbi.V9` — package major == anchor major per
  `tools/verify-version-sync.py`, consistent with §6.1's per-package lineage.)

With tag-triggered publishing (§5), the entire rename can land on `main` with
zero registry effect; the version tag is the human act.

---

## 7. Release gates (all agent-verifiable)

1.0.0 ships when every gate below passes. Each is one command, deterministic, no human judgment required.

**G1 — Vocabulary completeness.**
Authoritative: `QYL0200` reports **zero diagnostics** across the workspace. The analyzer checks telemetry *name positions* — `SetTag`/`AddTag`/event-name arguments, `ActivitySource` construction, `Meter.Create*` and metric-descriptor names — and its allowlist is the generated catalog, never a hardcoded list; a string literal in a name position that doesn't resolve to a generated constant is a build error. Smoke (cheap CI cross-check, not the gate): `grep -rn '"qyl\.' --include='*.cs'` scoped to the telemetry-emitting projects (producer family + collector source), excluding `eng/`, `tools/`, build assets, tests, `*.g.cs`, and the registry input → **0 hits in scope** — the expected result is zero, not "hits that don't count." Package IDs, directory names, and executable names live outside the scope by construction.

**G2 — One command, one diff.**
Edit one registry YAML line → run `./generate.sh` (Nuke target) → `git status` shows only intended generated changes. The nuke-and-regen proof: the full codebase effect of a vocabulary change is one command, deterministic.

**G3 — Closed loop.**
Emitting constants and `CollectorSemanticAttributeCatalog.g.cs` provably derive from the same registry revision (asserted by a test comparing generation provenance, not by convention).

**G4 — Analyzer enforcement live (negative proof).**
Deliberately introducing a hardcoded telemetry string in a name position fails the build via `QYL0200` — asserted by a compile-should-fail test, not by inspection. The corresponding prose rule is deleted from the instructions; the analyzer owns it.

**G5 — Boundary isolation, producer.**
The conformance app (`AddQyl()` + inbound span assertion on the qyl-owned source + loopback outbound call) passes **with no collector running** and with zero `Qyl.Collector.*` references. As amended by Alex at the launch review (in chat, 2026-07-27): G5 asserts the qyl-owned inbound source by its canonical name; the canonical scope name is a ledgered surface — the published name (`Qyl.OpenTelemetry.AutoInstrumentation`) until the first post-launch major (#12), the family name (`Qyl.Telemetry.AutoInstrumentation`) thereafter. Readers accept both from launch.

**G6 — Boundary isolation, collector.**
The collector's ingest/storage/query tests pass driven by a **plain OTLP client**, with zero producer-family references in the test project. If G5 or G6 ever requires the other side's packages, the boundary has leaked — release blocker.

**G7 — Dependency edges.**
Automated test asserts the actual package graph equals the §2 edge list exactly, and `InternalsVisibleTo` count in the producer family is 0. The assertion covers **every** project in the workspace, `internal/` included — a parallel producer implementation inside the collector repo is a forbidden edge and fails this gate, whatever directory it hides in.

**G8 — Guards fail closed.**
Startup tests: explicit self-export config → throws; endpoint absent with export composed → does not export (required-endpoint layer); discovery cannot re-enable itself in the collector's own composition; unwired health surface → throws.

**G9 — Standard gates green, unchanged.**
`-warnaserror` build, full test run, NativeAOT publish of collector and conformance app, ByteIdentity snapshots in the semconv repo, plus the new `qyl.*` projection snapshot — and PublicAPI baselines and the ABI-anchor verifier tokens in sync with the anchored namespace (§6.2).

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
