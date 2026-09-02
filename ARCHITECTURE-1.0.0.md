# qyl Architecture

**Normative.** This file is the single source of architectural truth for every
repository in the qyl workspace. `docs/component-taxonomy.html` is a view of it;
on conflict this file wins. Git history is the ledger: this document carries no
dates, no migration narrative, and no measurements. Code that contradicts it is a
defect at the owning repository, fixed by making the code match, and recorded in
§10 until the gate proves it. The document changes only when the architecture
changes.

Section numbers §2, §5, §6.1, §6.2, §7 and gate IDs G1–G11 are cited from code,
CI, and project files. They are stable identifiers.

---

## 0. Thesis

qyl is one schema-owned platform: **one wire, two generated loops, many
independently shipped artifacts. One graph, one truth, many artifacts.**

- **The wire is OTLP.** A producer stack lives inside a customer's process and
  ends at an OTLP exporter. The collector is a standalone process that begins
  where that exporter ends. No package crosses the wire.
- **Loop 1 — vocabulary.** One weaver registry generates the producer's
  constants and typed definitions *and* the collector's ingest catalog. qyl
  cannot emit telemetry its own collector does not recognise.
- **Loop 2 — contract.** One TypeSpec repository (`qyl-api-schema`) generates
  the collector's API surface *and* every first-party client of it, including
  the MCP tool shapes. No client can hold a shadow contract.

The code **declares and subscribes**; the runtime, the registry, and the
generators **produce**. Every rule in this document is owned by a compiler, an
analyzer, a generator, or a gate; a rule nothing enforces is a §10 gap, not a
convention.

```text
customer process                                     qyl collector process
┌────────────────────────────────────┐               ┌────────────────────────────────────┐
│ Qyl.Telemetry.SemanticConventions  │               │ services/qyl.collector             │
│ Qyl.Telemetry                      │     OTLP      │   ingest      :4317 gRPC  :4318    │
│ Qyl.Telemetry.AutoInstrumentation  │ ────────────► │   catalog     CollectorSemantic…   │
│ Qyl.Telemetry.Hosting   AddQyl()   │  /v1/traces   │   DuckDB      traces, logs, metrics│
└────────────────────────────────────┘  /v1/logs     │   API+health  :5100                │
        ▲                               /v1/metrics  └────────────────┬───────────────────┘
        │                                                             │ collector API
   loop 1 · weaver registry ──────────────────────────────────────────┤ loop 2 · qyl-api-schema
   one YAML → constants, definitions, catalog                         │ one TypeSpec → Qyl.Api.Contracts
                                                                      │             + @ancplua/qyl-api-schema
  qyl.at Worker ── OTLP logs ──►          ┌───────────────────────────┼───────────────────────────┐
  Qyl.Run.Workload (demo producer)        ▼                           ▼                           ▼
                                     qyl (CLI)                  qyl.dashboard           qyl.mcp/server ◄── MCP clients
                                     supervisor, Runner API                             qyl.mcp/workbench ──► external MCP
                                     :18889                                             :18888 (open world outward)
```

---

## 1. Processes

**Producer.** A customer application references one package,
`Qyl.Telemetry.Hosting`, calls `builder.AddQyl()`, and emits traces, metrics,
and logs over OTLP. The producer never stores, never queries, never validates
ingest, and knows the collector only as an endpoint URI.

**Collector.** One project, one process (`services/qyl.collector`). It listens
on OTLP gRPC `:4317` and OTLP/HTTP `:4318`, validates ingest against the
generated catalog, persists traces, logs, and metrics to DuckDB, and serves the
API, dashboard, and health surface on `:5100`. Metric points pass the same
registry-backed attribute policy span attributes do, and are stored as a series
index (`metric_series`) plus a narrow point table (`metric_points`); OTLP's
summary point is declined by name in a `partial_success` because its
pre-computed quantiles cannot be re-aggregated. Other OTLP signals have no
endpoint. The release and container artifact is NativeAOT;
the copy bundled into the `qyl` tool ships framework-dependent. The collector's
product code references no `Qyl.Telemetry.*` package.

**Self-telemetry.** A first-party .NET process instruments itself through the
published producer packages, exactly as a customer would — `Qyl.Telemetry.Hosting`,
or `Qyl.Telemetry` alone when it needs no auto-capture. The collector host does
this through `internal/qyl.instrumentation`. A private copy of the producer
composition is a forbidden edge wherever it hides (G7).

> **Self-export invariant.** The collector's own exporter never targets its own
> ingest ports, and "no endpoint" means "do not export". Three independent
> layers fail closed at startup: `CollectorSelfExportGuard.ThrowIfSelfExporting`
> on an explicit endpoint, discovery disabled in the collector's own
> composition, and `RequireConfiguredEndpoint` closing the exporter's silent
> `localhost` default. Weakening any layer is a release blocker (G8).

**Clients.** `qyl` (the CLI, `qyl up`) supervises the local stack — product
collector on `:5100`, diagnostics collector on `:5200`, OTLP on `:4317`/`:4318`
— exposes the Runner API on `:18889`, and is a client of the collector API,
never the API. `Qyl.Run.Workload` is the demo workload: a standalone producer
that fabricates GenAI, HTTP, and database telemetry for local runs.
`qyl.dashboard` presents product telemetry over HTTP. All of them reach the
collector only through generated contract clients (loop 2). Process spawning is
process-level, not a package edge.

**MCP plane.** `qyl.mcp/server` is the closed-world MCP server (npm
`qyl-mcp-server`, hosted at `mcp.qyl.at`): it projects the qyl model as MCP tools
over stored telemetry, with tool *shapes* generated from the contract and tool
*curation* authored. `qyl.mcp/workbench` is the MCP client runtime (loopback
`:18888`). Its own HTTP/SSE API is part of the contract; outward it is open
world — it connects to arbitrary external MCP servers and validates their
schemas at runtime by design. `qyl.mcp/dashboard` is the Workbench UI, served by
the Workbench host: its subject is MCP, its protocol is HTTP.

**Site.** `qyl.at` is a static Astro site on Cloudflare Workers. Its Worker's one
dynamic job is the same-origin Core Web Vitals endpoint, forwarded as OTLP logs
to the hosted collector — a producer with no .NET stack, proof that the wire is
the boundary.

---

## 2. Package family

### Producer family

| Package | Owns | Never contains |
|---|---|---|
| `Qyl.Telemetry.SemanticConventions` | Generated stable vocabulary and the definition types (`MetricDefinition<T>`, `SpanDefinition<T>`, `EventDefinition`, `EntityDefinition`) | `Activity`, `Meter`, DI, OTLP |
| `Qyl.Telemetry.SemanticConventions.Incubating` | Generated unstable vocabulary, qyl-owned `qyl.*` entries and scope names, GenAI payload schemas | Same |
| `Qyl.Telemetry.SemanticConventions.SourceGeneration` | The Roslyn generator that turns the registry into the packages above and into consumer-side definitions | Runtime code |
| `Qyl.Telemetry` | Primitives: `ActivitySource` and `Meter` ownership, scope names, typed instruments created from definitions, shared options, session identifiers, the explicit `StartOperation`-style API | Exporters, discovery, interception, DI |
| `Qyl.Telemetry.AutoInstrumentation` | Automatic capture: the declarative interceptor catalog and its generated code, `DiagnosticSource` listeners, framework hooks, bootstrap and DI registration of capture | Exporters, discovery, resource config, the consumer's OTel pipeline |
| `Qyl.Telemetry.AutoInstrumentation.<Integration>` | Per-integration capture (`EntityFrameworkCore`, `SqlClient`, …) that needs the integrated library at compile time | Same as parent |
| `Qyl.Telemetry.Hosting` | The composition root: `AddQyl()`, source and meter subscription, resource identity, processors, OTLP export, collector discovery | Telemetry of its own, storage, anything server-side |

Every runtime package in the producer repository is `net10.0`, nullable,
trimmable, and AOT-compatible; the semantic-convention packages also target
`netstandard2.0` for generator and analyzer hosts. Instrumentation dispatch is
compile-time: declarations on the runtime (`[QylIntegration]`, `[QylIntercept]`)
drive the generator; nothing scans, rewrites IL, or reflects at runtime.

### Collector

The collector is one project, `services/qyl.collector` (root namespace
`Qyl.Collector`; `Ingestion`, `Storage`, `Hosting`, `Grpc` are namespaces, not
packages). It owns OTLP ingest, ports, the pipeline,
`CollectorSemanticAttributeCatalog.g.cs`, DuckDB persistence, auth, process
bootstrap, and guards. It is a process, never a package: `Qyl.Collector.*` is a
forbidden package reference everywhere.

### Platform

| Component | Owns | Ships as |
|---|---|---|
| `Qyl.Api.Contracts` + `@ancplua/qyl-api-schema` | The two generated faces of the one API contract | NuGet + npm, generated, never hand-edited |
| `qyl` | Local stack supervision, Runner API; client of the collector API | NuGet global tool (`qyl/packages/Qyl.Cli`) |
| `Qyl.Run.Workload` | Demo workload producer | .NET process (`qyl/packages/Qyl.Run.Workload`) |
| `qyl.dashboard` | Product telemetry UI | Web bundle (`qyl/services/qyl.dashboard`) |
| `qyl.mcp/server` | Closed-world MCP projection and its viewer bundles | npm `qyl-mcp-server`, Railway, `mcp.qyl.at` |
| `qyl.mcp/workbench` | MCP client runtime; serves the Workbench UI | Node loopback process, `:18888` |
| `qyl.mcp/dashboard` | Workbench UI | Vite bundle, served by the Workbench host |
| `qyl.at` | Public site and vitals Worker | Cloudflare Workers Static Assets |

### Dependency edges (exhaustive — anything not listed is forbidden)

Producer repository:

```text
Qyl.Telemetry.SemanticConventions            → (no Qyl.* edge)
Qyl.Telemetry.SemanticConventions.Incubating → (no Qyl.* edge)

Qyl.Telemetry
└── Qyl.Telemetry.SemanticConventions

Qyl.Telemetry.AutoInstrumentation
├── Qyl.Telemetry
├── Qyl.Telemetry.SemanticConventions
└── Qyl.Telemetry.SemanticConventions.Incubating

Qyl.Telemetry.AutoInstrumentation.<Integration>
├── Qyl.Telemetry.AutoInstrumentation
└── the integrated library

Qyl.Telemetry.Hosting
├── Qyl.Telemetry
├── Qyl.Telemetry.AutoInstrumentation
├── OpenTelemetry.Extensions.Hosting
└── OpenTelemetry.Exporter.OpenTelemetryProtocol
```

qyl repository (executable twin: `eng/build/BuildDependencyEdges.cs`):

```text
eng/build                        → SemanticConventions, Incubating        (catalog generation input)
eng/tools/QylSdkConformance      → Qyl.Api.Contracts
internal/qyl.instrumentation     → Qyl.Telemetry.Hosting, Qyl.Api.Contracts,
                                   SemanticConventions, Incubating        (self-telemetry only)
packages/Qyl.Cli                 → Qyl.Api.Contracts,
                                   SemanticConventions, Incubating       (scope and event names)
packages/Qyl.Run.Workload        → SemanticConventions.SourceGeneration
services/qyl.collector           → Qyl.Api.Contracts
tests/Qyl.Sdk.Conformance        → Qyl.Telemetry.Hosting                  (released package, never a project)

forbidden everywhere             : Qyl.Collector.*, Microsoft.Extensions.AI.*,
                                   Microsoft.Agents.*, ANcpLua.Agents.*
no ProjectReference on services/qyl.collector except tests/Qyl.Collector.Tests
```

Clients:

```text
qyl.mcp/server, qyl.mcp/workbench, qyl.mcp/dashboard, qyl.dashboard → @ancplua/qyl-api-schema (exact pin)
qyl.at                                                              → none (OTLP producer)
```

**`InternalsVisibleTo` across the producer family: 0.** An IVT marks a
responsibility living in the wrong package. Primitives belong in
`Qyl.Telemetry`; capture lanes belong in one `AutoInstrumentation` assembly;
per-integration packages consume public, generated surfaces.

**No third-party metrics-, logging-, or DI-authoring package enters the producer
family.** The registry generates the typed instruments; the runtime's
`System.Diagnostics` APIs are the only instrument primitives.

---

## 3. Consumer contract

Default — one package, one line:

```xml
<PackageReference Include="Qyl.Telemetry.Hosting" />
```

```csharp
builder.AddQyl();
```

Manual instrumentation only, no auto-capture, no pipeline opinion:

```xml
<PackageReference Include="Qyl.Telemetry" />
```

Own OTel pipeline with auto-capture: reference
`Qyl.Telemetry.AutoInstrumentation`, call `AddQylAutoInstrumentation()`, and
subscribe the qyl sources and meters explicitly. Hosting is convenience, never a
requirement (G5).

Hosting's public surface is exactly the namespace `Qyl`, the method `AddQyl()`,
and `QylSdkOptions`. Every other public surface in the family is pinned by a
PublicAPI baseline.

---

## 4. The two loops

### Loop 1 — vocabulary

Single source: the weaver registry — pinned upstream OpenTelemetry semantic
conventions, the pinned GenAI conventions, and the qyl-owned `qyl-registry.json`,
merged by `generate.sh` into one resolved registry. Every entry carries
stability, brief, unit, and requirement level; qyl's own vocabulary and scope
names are first-class beside upstream.

One command generates:

1. Stable and incubating constant classes, enum value sets, and doc comments.
2. Typed definitions: `MetricDefinition<TInstrument>`, `SpanDefinition<TKind>`,
   `EventDefinition`, `EntityDefinition`, each carrying name, unit, brief,
   stability, deprecation, and attribute references with requirement levels.
3. Scope names (`QylTelemetryNames.Scopes`) and typed instruments created from
   definitions.
4. `CollectorSemanticAttributeCatalog.g.cs` and the analyzer's registry facts.

#### One owner per fact

Every fact about an integration lives in exactly one place; the runtime only
declares the join between owners, and the generator produces the plumbing.

| Fact | Owner |
|---|---|
| Instrumentation id, signal, environment toggle | The upstream OpenTelemetry .NET auto-instrumentation contract, `docs/contracts/otel-dotnet-auto-60.upstream.yaml` — one row per upstream promise |
| Domain value, attribute keys, span kind, required attributes, scope name | The registry: `InstrumentationDomainValues`, `SpanDefinition<TKind>`, `QylTelemetryNames.Scopes` |
| Which upstream rows are implemented | The generator's emitted interceptor manifests (`contractKeys`); the coverage matrix is upstream rows ⨝ manifests, and a row with no manifest is `not_implemented` by absence |
| Body template | The helper's own signature — a `Start` returning `Activity?` wraps, a same-named forwarding overload forwards, a declared metric records duration alongside |

GraphQL is the one recorded exception to registry-owned span kind: the
registry's `graphql.server` span group is development-stability, and the
GraphQL span nests inside the ASP.NET Core server span, so qyl keeps
`ActivityKind.Internal` for it by decision (2026-09-02). The reason is
recorded in the `Qyl.Telemetry.AutoInstrumentation` CHANGELOG under
`## [10.0.0] - 2026-09-02` → `### Changed`.

The generator reads the contract as an additional file and emits typed
promises (`OtelDotnetAuto.Traces.HttpClient`, `OtelDotnetAuto.Logs.ILogger`):
the signal is the type, so a logs promise cannot be handed to a span-starting
declaration. An integration is three typed operands and one join:

```csharp
[QylIntegration(OtelDotnetAuto.Traces.HttpClient, InstrumentationDomainValues.HttpClient, HttpSpans.Client)]
[QylIntercept("System.Net.Http.HttpClient", Shape = QylShapes.HttpClient, Start = nameof(Send))]
public static class QylInterceptedHttpClient
{
    public static Activity? Send([QylFromArgument(0)] HttpRequestMessage request) => …;
}
```

The only hand-written code per integration is the helper body that reads
library arguments, because nothing else can know that `request.Method` is the
HTTP method. An analyzer checks that each helper's enrichment covers its
definition's required attributes.

#### Signals

**Traces.** Spans come from the declarative interceptor catalog above, from
`DiagnosticSource` listeners, and from library-native `ActivitySource`s that
qyl subscribes to by name. Span names are computed from tags by fixed rules;
span kinds, required attributes, and scope names are registry facts. No tag
key or source name is a hand-typed literal (G1). The one recorded exception is
GraphQL: its `graphql.server` span group is development-stability and the span
nests inside the ASP.NET Core server span, so qyl keeps `ActivityKind.Internal`
for it by decision (2026-09-02) rather than emitting a second server span per
request — see `## [10.0.0] - 2026-09-02` → `### Changed` in the
`Qyl.Telemetry.AutoInstrumentation` CHANGELOG.

**Metrics.** The producer never re-produces an instrument the runtime or a
library already publishes. `System.Runtime`, `Microsoft.AspNetCore.*`,
`System.Net.*`, `Npgsql`, `NServiceBus.*` and the GenAI meters are subscribed by
name; the subscription list is data, gated per integration by
`QylAutoInstrumentationOptions`. qyl-owned instruments are created from their
`MetricDefinition<T>` — name, unit, brief, and required tags come from the
registry; the typed recorder is generated. `Meter.Create*` with a literal name
does not exist in the family.

**Logs.** Logs are the logs signal. `ILogger` records flow through the
OpenTelemetry logging provider to `/v1/logs` with trace context attached by the
SDK; third-party loggers reach the same signal through their OTLP or `ILogger`
bridges. A logs promise accepts only a `LogRecord`-producing declaration —
Hosting's provider registration for `ILogger`, a target or appender
declaration for a third-party logger — and a promise with none is
`not_implemented` by absence. A log is never a span, and no `log.*` span
attribute exists; the type system rejects the lane before a product decision
is needed.

#### Attributes on the wire

Every attribute key the producer emits is a generated constant (G1) and
therefore a catalog key (G3). The collector persists a key only if
`eng/config/collector-semantic-policy.json` allowlists it — by registry
namespace prefix per signal, or by explicit key — and never if the policy denies
it (PII, secrets, payload text, headers). Every prefix and key in the policy is
verified against the catalog when the catalog is generated; development keys
are the one exception, and a development key the registry does not know is a
§10 gap. A key the producer emits and the policy drops is a policy defect,
fixed in the policy; a producer never works around it.

Consequences that are product claims:

- It is structurally impossible for qyl to emit telemetry its own catalog does
  not know. Semantic-convention compliance, including for `qyl.*`, is proven at
  compile time.
- Registry drift surfaces as a build or snapshot failure, never as a stale
  string on the wire.
- No hand-maintained vocabulary exists anywhere in the workspace.

### Loop 2 — API contract

Single source: `qyl-api-schema` (TypeSpec). Its emitters produce
`Qyl.Api.Contracts` (.NET: the collector serves it, the CLI calls it) and
`@ancplua/qyl-api-schema` (TypeScript types, JSON Schema, OpenAPI: the MCP
server, the Workbench, and both dashboards consume it). Every consumer pins the
exact version; the release tag — or an explicit dispatch input — is the only
version source, and committed files carry none.

For the MCP server the rule has two layers, because MCP tools are not REST
endpoints:

- **Shapes are generated.** Every tool input/output shape, request, response,
  and path imports from the generated artifacts. A hand-declared shape is a
  verifier failure (G10a).
- **Curation is authored.** Which tools exist, their names, descriptions,
  examples, pagination and summarisation, and which contract operations they
  compose is product design, referencing generated shapes only. 1:1
  endpoint→tool mirroring is a non-goal. The agent-visible tool surface is
  pinned to the contract revision by the tool-manifest snapshot (G10b).
- **Revision handshake, fail-closed.** The collector advertises its contract
  revision; the MCP server compares it to the revision baked into its artifacts
  at startup and refuses to serve on mismatch (G10c).

The Workbench's own API is in the contract. The external MCP servers it
connects to are not, and never will be: an open-world client's job is servers
with no shared static contract.

### Workflow state

Two owners, no third copy of truth. `qyl-api-schema` owns every public workflow
HTTP, SSE, and MCP shape — branded identifiers, opaque cursors, closed
projection-status variants, structured `deleted` / `cursor` / `unavailable` /
`corrupt` errors. The collector owns the private persistence:

- The append-only DuckDB **journal** is the sole authoritative record of
  workflow history. Run summaries, graphs, nodes, edges, statistics, manifests,
  repair state, and checkpoint files are derived and may be discarded and
  rebuilt. Durable deletion is a tombstone: it blocks new events and stale
  publication and never erases journal history.
- Each run generation has at most one committed manifest referencing one
  immutable, content-addressed checkpoint. A checkpoint is trusted only when
  generation, included journal position, canonical input hash, projector
  fingerprint, configuration fingerprint, format version, byte length, and
  SHA-256 address all match; reads then continue incrementally from that
  position.
- Checkpoint replacement is write → flush → close → validate → CAS-publish. The
  previous manifest stays active until the CAS succeeds; a loser reloads the
  winner and cannot overwrite it. One hosted reconciliation owner validates
  manifests, schedules rebuilds for missing, corrupt, stale, or incompatible
  state, and removes orphans after a safety interval. It never edits the
  journal.
- The projection runtime coalesces demand per generation, distinguishes
  rotation from deletion, transfers waiters to a live successor, and preserves
  cancellation ownership. Every DuckDB error type has an explicit
  classification: retryable failures get bounded storage-level retries;
  constraint, schema, corruption, and programmer failures never become caller
  retry loops.
- Storage APIs divide by semantics. Generated appenders — reusable rows, static
  callbacks, native `byte[]` for BLOBs — own append-only ingestion. Typed,
  parameterised, transactional SQL owns everything needing sequence allocation,
  idempotency, `ON CONFLICT`, affected-row counts, CAS, or `RETURNING`.
  Streaming Arrow with asynchronous batches, disposed at the generated boundary
  with cancellation propagated, owns bulk reads; point reads stay typed
  ADO.NET.
- The private schema and access paths are generated from one metadata model —
  canonical DDL, stable column order and types, appender writers, Arrow
  mappings, verifier metadata — with authoritative and disposable SHA-256
  schema identities in `qyl_schema_meta`. Empty databases are created directly;
  disposable tables are dropped and recreated on mismatch; a mismatch on
  non-empty authoritative tables fails closed and requires an explicit,
  operator-visible reset or a separately proven journal-preserving
  replacement. There is no migration framework, no persisted graph table, no
  replay-on-read, no hand-written public workflow DTO, no caller retry patch,
  no hand-written hot-path storage adapter.
- Checkpoint filesystem containment is platform-owned (pinned directory
  handles and no-follow atomic creates on Linux/macOS; rooted validation with
  reparse-point rejection, atomic create-new, and no-overwrite move on
  Windows). Every published RID keeps the same journal and checkpoint
  behaviour; none falls back to memory-only derived state.
- Structured logs cover journal commits, projection lifecycle and positions,
  checkpoint validation and CAS outcomes, repairs, and storage classifications
  — never workflow payloads or secrets.

Arrow, DuckDB types, rows, hashes, and checkpoints never cross the public
contract boundary.

---

## 5. Enforcement over convention

Every rule the compiler can own, the compiler owns, and the corresponding prose
is deleted from `AGENTS.md`. Verification artifacts — analyzers, verifiers,
snapshots, PublicAPI baselines, ABI anchors — are contracts: they change through
their owning bump rule and regeneration, never by loosening so a change passes.

| Rule | Enforcer | Failure |
|---|---|---|
| No literal telemetry name in a name position | `QYL0200` / `QYL0201` (Error), allowlist = the generated registry facts; `eng/scripts/g1-vocabulary-smoke.sh` as cross-check | Build error / CI failure |
| Package graph equals §2 exactly; collector reachable only via API | `eng/build/BuildDependencyEdges.cs` (G7, G11) | Verify failure |
| Public surface and generated-code ABI stable | PublicAPI baselines, `QylGeneratedCodeAbi`, `tools/verify-version-sync.py` | Build / verifier failure |
| Collector never exports to itself; "no endpoint" = "no export" | `CollectorSelfExportGuard` + discovery off + `RequireConfiguredEndpoint` (G8) | Startup exception |
| Health surface wired | `CollectorHealthGuard.ThrowIfHealthSurfaceUnwired` | Startup exception |
| Catalog current; policy catalog-backed | `VerifyCollectorSemanticAttributeCatalog` (regenerates, checks every policy prefix and key against the catalog); `VerifyCollectorSemanticPolicyIsCatalogBacked` (ingest code reads only generated catalog members) | Verify failure |
| Storage paths generated | `VerifyCollectorStorageTablesUseGeneratedDdl`, `VerifyCollectorStorageWritesUseGeneratedBatchHelper` | Verify failure |
| Registry drift visible | ByteIdentity snapshots and the `qyl.*` projection snapshot | Snapshot diff |
| NativeAOT holds | AOT publish of the collector and the conformance app; the producer goal script (`tools/verify-aot-autoinstrumentation-goal.py`) | Publish failure |
| No hand-declared contract shapes | `verify-generated-shapes.mjs` (G10a), `BuildCliContractLoop.cs` (G10a, CLI), tool-manifest snapshot (G10b) | Verifier / snapshot failure |
| MCP server ↔ collector contract revision | Startup handshake (G10c) | Startup exception |
| Package publication is a human act | Publish lanes run on a version tag, a GitHub release, or an explicit manual dispatch, through OIDC trusted publishing; `main` builds and verifies. The site deploys from `main` — a deploy mints nothing immutable | No publish without the act |

---

## 6. Identity

### 6.1 Versioning

Identity is per-package lineage; a launch is an event, not a number.

- The collector and the `qyl` tool ship the product line.
- The producer family ships one version; its major equals the
  `QylGeneratedCodeAbi` major (`verify-version-sync`).
- The semantic-convention packages ship their own line, pinned to an upstream
  semconv schema version in `Version.props`.
- The API contract ships its own line, revision-hash-pinned; every consumer
  pins the exact version in the same commit that consumes it.

Published packages are immutable. Superseded package IDs (`Qyl.Sdk`,
`Qyl.OpenTelemetry.*`) stay frozen and are unlisted, never shimmed — a shim
would be the second contract owner the boundary forbids. Cross-repository
changes land owner-first: publish the owner, prove the artifacts are indexed,
then consume.

### 6.2 ABI

Consumers compile generated code into their own assemblies. The generated-code
namespace and the `QylGeneratedCodeAbi` anchor are therefore consumer-compiled
ABI: never renamed or re-derived within a major, and a breaking change to
generated code bumps the anchor and the package major together. A package
rename is an **ABI-free slice** when the namespace and the entry point survive
(`Qyl.Sdk` → `Qyl.Telemetry.Hosting` kept namespace `Qyl` and `AddQyl()`).

The inbound scope name is a registry fact (`scope_names` →
`QylTelemetryNames.Scopes`), and is `Qyl.Telemetry.AutoInstrumentation` as of
AutoInstrumentation 10.0.0 — the rename from
`Qyl.OpenTelemetry.AutoInstrumentation` shipped with that producer major, which
is how a registry change is always shipped; readers accept both names across
it. The conformance assertion in `tests/Qyl.Sdk.Conformance/Program.cs` pins
the new name, flipped in the same commit as the 10.0.0 pin bump.

ByteIdentity snapshots, PublicAPI baselines, and pinned verifier tokens
regenerate in the same commit as the change: one change, one regeneration, one
diff.

### 6.3 Release train

The lineage every repository converges to. "Shipped" is the registry; "next"
is the owner's version property. A row whose next differs from shipped is open
and closes with its tag; a consumer row closes when the pin equals the owner.

| Line | Shipped | Next | Owner of the number |
|---|---|---|---|
| Producer family (`Qyl.Telemetry.*`) | 10.1.0 · ABI `V10` | 10.1.0 · ABI `V10` | `Directory.Build.props` `<Version>`, `QylGeneratedCodeAbi.cs` (`refactor/elegance`) |
| Semantic conventions | 7.1.1 | — | `Directory.Build.props` `<VersionPrefix>` |
| Collector + `qyl` tool | 2.0.0 | — | `qyl/Version.props` `<QylVersion>` |
| API contract | 8.0.0 | — | release tag |
| MCP plane (`qyl-mcp-server`, root, workbench) | 3.0.0 · 1.1.1 · 1.1.1 | — | `qyl.mcp/*/package.json` |
| Site (`qyl.at`) | 1.0.0 | — | `qyl.at/package.json` |

| Consumer → owner | Pinned | Target | Where |
|---|---|---|---|
| Producer → semantic conventions | 7.1.1 | 7.1.1 — in sync | producer `Directory.Packages.props` |
| Collector → producer family | 10.1.0 | 10.1.0 | `qyl/Version.props` `<QylTelemetryVersion>` |
| Collector → semantic conventions | 7.1.1 | 7.1.1 — in sync | `qyl/Version.props` `<QylSemanticConventionsVersion>` |
| Collector, MCP, dashboards → API contract | 8.0.0 | 8.0.0 — in sync | `qyl/Version.props` `<QylApiContractsVersion>`; `package.json` exact pins |

The producer → semantic-conventions edge was a migration, not a bump: the
definition types ship as package types from 6.0.0 on, and `QYLSG001` is an
error when a definition surface is used without the package reference. It
closed the §10 typed-instrument gap.

**Held pins** are design, not debt; each carries its reason beside the number
in the owning file:

| Pin | Held at     | Why |
|---|-------------|---|
| `Microsoft.OpenApi` | 2.12.2      | `Microsoft.AspNetCore.OpenApi` 10.x declares `[2.x, 3.0.0)` and its generator assigns a member read-only in 3.x (CS0200); fixed in .NET 11 preview4+. `qyl/Version.props` |
| `MassTransit.RabbitMQ` | 8.5.10      | 9+ requires a runtime licence; the verifier stays on the no-secret line. Producer `Directory.Packages.props` |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | 5.6.0       | Ships on its own cadence; no release matches the compiler line, so it has its own property. `qyl/Version.props`, producer `Directory.Packages.props` |
| `SQLitePCLRaw.lib.e_sqlite3` | 3.53.3      | Overrides `Microsoft.Data.Sqlite`'s vulnerable native transitive (GHSA-2m69-gcr7-jv3q). Producer `Directory.Packages.props` |
| `Qyl.Telemetry.SemanticConventions.Analyzers` | 7.1.1, unreferenced here | The producer references it from 10.1.0 under the instrumentation-library opt-out; no project in this repository references it until consumer evidence and behaviour coverage exist (§10, `QYL0200`) |
| `OpenTelemetry.Instrumentation.Runtime` | absent      | The runtime's `System.Runtime` meter is subscribed directly (§4); on .NET 9+ the package is a forwarder to it |

---

## 7. Gates

Each gate is one command with a deterministic result.

- **G1 — Vocabulary completeness.** `QYL0200` reports zero diagnostics across
  the workspace; `eng/scripts/g1-vocabulary-smoke.sh` finds zero `"qyl.`
  literals in telemetry-emitting source.
- **G2 — One command, one diff.** Edit one registry line, run the generator,
  and `git status` shows only intended generated changes.
- **G3 — Closed loop.** The emitted constants and
  `CollectorSemanticAttributeCatalog.g.cs` derive from the same registry
  revision, asserted by generation provenance.
- **G4 — Analyzer enforcement live.** A deliberately hardcoded telemetry name
  produces an Error-severity `QYL0200`, asserted by an analyzer test.
- **G5 — Producer isolation.** The conformance app — `AddQyl()`, an inbound
  span on the qyl-owned scope, a loopback outbound call — compiles against the
  released `Qyl.Telemetry.Hosting` with zero `Qyl.Collector.*` references and
  asserts the scope by its registry name.
- **G6 — Collector isolation.** Ingest, storage, and query tests pass driven by
  a plain OTLP client, with no direct producer-family reference.
- **G7 — Dependency edges.** The package graph equals §2 for every project,
  `internal/` included, and the producer family's IVT count is 0.
- **G8 — Guards fail closed.** Self-export config throws; absent endpoint with
  export composed registers no exporter; an unwired health surface throws.
- **G9 — Standard gates.** `-warnaserror` build, full test run, NativeAOT
  publish of the collector and the conformance app, snapshots and PublicAPI
  baselines in sync with the anchored ABI.
- **G10 — Contract loop closed.** (a) Zero hand-declared shapes in
  `qyl.mcp/server` and the CLI outside generated files; (b) the tool-manifest
  snapshot is pinned to the TypeSpec revision; (c) revision mismatch between
  the MCP server and the collector throws at startup.
- **G11 — Client isolation.** The CLI, `qyl.mcp/*`, and the dashboards carry
  zero `Qyl.Collector.*` references and no project reference on the collector.

**Scoreboard:** literal telemetry names 0 · regeneration commands 1 (vocabulary)
+ 1 (contract) · IVT count 0 · cross-boundary package references 0 ·
hand-declared contract shapes in first-party clients 0.

---

## 8. Non-goals

- No backcompat shims for retired package IDs.
- No OpenAPI or contract generation from the collector; the contract lives in
  `qyl-api-schema` and flows in. No 1:1 endpoint→tool mirroring in the MCP
  server.
- No static contract for the external MCP servers the Workbench connects to.
- No runtime registry loading, no reflection, no IL rewriting, no plugin model.
  A capability that cannot be generated or compiled in waits.
- No multi-collector federation, no clustering. One binary, one DuckDB.
- No raw-point metric reads. Metric queries name a metric, match attributes,
  and give a range and a step; the collector aggregates into buckets server-side
  because the primary consumer is an agent over MCP, and a compact bucketed
  answer is both the cheaper payload and the one it can reason about.
- No OTLP summary storage. Its pre-computed quantiles can be neither
  re-aggregated over a window nor merged across series, so it is declined by
  name in a `partial_success`; half-storing a signal is worse than declining it.
- No second histogram shape. An exponential histogram is materialized into the
  same explicit bucket vector an OTLP histogram uses, so storage and queries
  carry one shape and percentiles work uniformly.
- No log-as-span lane. Logs are logs.

---

## 9. One-line definitions

> `Qyl.Telemetry.SemanticConventions` tells qyl **what** vocabulary to emit.
> `Qyl.Telemetry` defines qyl's telemetry **primitives** and explicit API.
> `Qyl.Telemetry.AutoInstrumentation` implements **how** telemetry is captured automatically.
> `Qyl.Telemetry.Hosting` composes the producer pipeline and **ends at the OTLP exporter**.
> The collector **begins where the exporter ends**: receives, validates against the shared registry, stores, serves.
> `Qyl.Api.Contracts` and `@ancplua/qyl-api-schema` are the **two generated faces of one contract**.
> `qyl` is a **client** of the collector API and the supervisor of the local stack.
> `Qyl.Run.Workload` is the **demo workload** — a producer, nothing more.
> `qyl.mcp/server` **projects** the qyl model as MCP tools — shapes generated, curation authored.
> `qyl.mcp/workbench` is the MCP client runtime — contract inward, **open world** outward.
> `qyl.mcp/dashboard` presents the Workbench — subject MCP, protocol HTTP.
> `qyl.at` is the public site and a **producer** on the same wire.

---

## 10. Gaps

Each line is deleted when its gate proves it. Nothing else in this document
describes a gap.

- `Qyl.Telemetry` does not exist; its primitives live in
  `Qyl.Telemetry.AutoInstrumentation`, and the producer family carries nine
  `InternalsVisibleTo` entries.
- `Qyl.Telemetry.AutoInstrumentation.DiagnosticListeners` and
  `Qyl.Telemetry.AutoInstrumentation.Hosting` ship as separate packages, and
  `AddQylAutoInstrumentation()` lives in the latter; both fold into
  `Qyl.Telemetry.AutoInstrumentation`.
- The producer pins semantic conventions 7.1.1, which carries the typed
  definitions, but `QylMetricNames` still hand-types the two qyl instrument
  names and no analyzer checks enrichment against required attributes.
- The upstream contract YAML is read only by `tools/generate-contract-artifacts.py`;
  `QylAutoInstrumentationIds`, `QylAutoInstrumentationSignal`,
  `QylInstrumentationDomains`, and `QylInterceptorBody` restate the contract,
  the registry, and the helper signature by hand, and
  `docs/contracts/qyl-aot-ownership.yaml` is hand-edited instead of joined
  from the emitted manifests.
- Three system values (`dotnet_wcf`, `masstransit`, `nservicebus`) are
  literals in the producer and in no registry: extend the registry or declare
  them local — undecided.
- Eight development keys (`web.vital.*`, `page.route`, `navigation.type`,
  `browser.*`) exist only in the collector policy, and the `qyl.at` Worker
  emits them as literals; they belong in `qyl-registry.json`.
- `Qyl.Telemetry.SemanticConventions.Analyzers` is referenced by the producer
  since 10.1.0 under the instrumentation-library opt-out, and by no collector
  project, so `QYL0200` still runs only in the semconv repository's tests;
  G1 is carried by the smoke script over two directories.
- The CLI and `Qyl.Run.Workload` compose OpenTelemetry by hand with literal
  endpoints instead of consuming the published producer packages.
- G2, G3, and G7's IVT clause have no enforcement — no gate in either
  repository counts `InternalsVisibleTo`; G5, G6, and G9 are enforced without
  the ID in code.
- §6.3 is hand-maintained; no verifier reads it against the version
  properties it cites.
- `docs/component-taxonomy.html` predates this text.
