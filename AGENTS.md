# qyl engineering contract

This is the only editable agent/contributor instruction file in this repository.
`CLAUDE.md` is a symlink to it. Keep public explanation in `README.md`, released history in release notes, and
executable truth in code, schemas, generators, and tests. Do not add progress diaries, repair prompts, handoff
documents, or a second rules file — every duplicated statement is a future contradiction.

## Where the law lives

`ARCHITECTURE-1.0.0.md` in this repository is the normative architecture:
component taxonomy, boundary law, the exhaustive dependency-edge list, the two generated loops, and gates G1–G11. Every
other qyl repository points at it.
`docs/component-taxonomy.html` is a view of the same content plus the target ↔ today naming ledger; on any conflict the
Markdown wins.

This file does not restate the architecture. If you find yourself needing a fact about boundaries, edges, loops, or
gates, read it there — a paraphrase here would be a second contract owner, which is the exact failure the architecture
forbids.

**Migration state.** This file and the architecture speak target names (`Qyl.Telemetry.*`, `Qyl.Collector.*`). Until the
rename PR lands, the code uses the today-names listed in the ledger (`docs/component-taxonomy.html` §2); map through the
ledger, don't guess. The ledger is migration scaffolding and is deleted when the rename lands (architecture §9).

## Definition of done

The standing goal is complete when gates G1–G11 pass, the `Ci` target below is green, and every touched repository is
clean and pushed. Gates are the arbiter:
a claim of completion without the corresponding gate output is not evidence.

Two classes of action are human-gated; everything else is autonomous:

- **Registry-irreversible steps** — publishing to nuget.org/npm and unlisting package IDs. Prepare, verify, and stop
  with the exact command ready.
- **Amending the decree** — changing what a gate *means* in
  `ARCHITECTURE-1.0.0.md`. If a gate is defective, annotate the defect in place with measured evidence and stop; rewrite
  only with explicit authorization in the goal. (Implementing a gate is normal work; redefining it is not.)

When you deviate from an instruction, say so and say why — a reasoned deviation report is worth more than silent
compliance and far more than silent deviation.

## Fail-closed invariants (placement and reasoning)

The architecture states the self-export invariant; these are the layers in this codebase and why each exists. A rename
must move them intact:

- `CollectorSelfExportGuard.ThrowIfSelfExporting` — sits directly after
  `AddQylCollectorCore` because that is where the ports become known and nothing is bound yet. It reads the explicit
  endpoint, so it is silent when nothing set one.
- `EnableAutoDiscovery = false` in the collector's own service defaults — stops the process from *probing* for a
  collector (itself).
- `RequireConfiguredEndpoint = true` on the hosting options — the layer the other two miss: without it the OTLP exporter
  falls back to its `localhost`
  default, which is this process's own ingest port. This flag makes "no endpoint" mean "do not export".
- `CollectorHealthGuard.ThrowIfHealthSurfaceUnwired` — separate concern, same fail-closed pattern.

The collector consumes the producer stack for self-telemetry only, through the published hosting package like any other
application — it does not carry its own copy of the composition logic. What stays on this side of the wire is the part
the hosting package cannot know: health checks and endpoints, exception capture, Kestrel and JSON conventions, and which
of this application's own endpoints are span noise (`HealthProbeSpanFilter`). Re-deriving OTel wiring, an ActivitySource
inventory, or collector discovery here is how the original fork started — the testable consequence is gate G6: the
collector fully exercisable with a plain OTLP client and no qyl producer packages.

## Delivery

qyl is beta. Work directly on `main`, preserve unrelated user changes, run the repository gates, make one intentional
commit per coherent repository change, and push it. Generated files are changed through their schema or generator and
regenerated in the same commit. Unpublished surfaces may converge directly; published package versions are immutable and
move through new versions (architecture §6.1 owns the rename-versioning policy).

On the wire, metrics are counted, discarded, and acknowledged with
`partial_success` — implement that behavior exactly; the product scope behind it is stated in the architecture (§1, §6).

## Contract ownership — repo-specific consequences

Loop 2 (architecture §4) owns the product-API rule. The consequences inside this repository:

- Do not declare parallel public DTOs in the collector, the CLI runtime, the dashboard, or MCP code. If an internal
  shape must cross an HTTP, gRPC, MCP, streaming, or generated-client boundary: change TypeSpec first, regenerate
  `Qyl.Api.Contracts` and client artifacts, then map to the generated contract. Accessibility modifiers do not decide
  contract status — anything serialized across a boundary is a contract.
- OTLP ingestion's wire contract is the official OpenTelemetry protobuf schema. Vendoring a pinned upstream `.proto`
  input is allowed; redefining it as a qyl-owned DTO hierarchy is not.
- Storage rows, ingest batches, query models, and projections may be owned locally precisely because they never cross a
  boundary.

## Implementation rules

- A public capability needs an executable owner: a product call path, an owned downstream consumer, or a conformance
  application exercising the complete contract. Mock-only tests and imaginary consumers are not acceptance evidence.
- Reuse a released, AOT-compatible upstream implementation when it satisfies the contract. Implement a missing gap only
  when qyl needs it, and prove it through a complete executable vertical.
- Tests and fixtures use real protocol types, valid programmatically generated data, or captured-and-sanitized
  datasets — hand-shaped JSON, substring checks over binary payloads, and echo-mocks prove nothing about
  interoperability.
- `Version.props` owns the product version and shared package-version properties; `Directory.Packages.props` owns the
  central `PackageVersion`
  entries that consume them. Hardcoded versions elsewhere will drift.
- The toolchain is the `global.json` SDK (`10.0.302`, `latestFeature`) and C# 14. Interceptors are supported on this
  SDK; use the current Roslyn APIs.
- Native AOT is the collector's publish contract (`QylAot` defaults on; the Dockerfile publishes the native lane).
  `eng/scripts/collector-aot-smoke.sh`
  is that lane's executable owner; `-p:QylAot=false` is the JIT diagnostic build with full analyzer enforcement.
- HTTP header attributes are denied at the collector persistence boundary unless the exact span key is in the small
  generated safe-header allowlist. Never persist `Authorization`, cookies, `Mcp-Param-*`, or arbitrary tool-defined
  header names in spans, logs, resources, entities, fixtures, or exception evidence — a leaked credential in stored
  telemetry is unrecoverable.
- Never hand-edit generated C#, protobuf output, TypeScript contracts, or generated reports. Analyzer release manifests
  are maintained inputs and change with their analyzer rules.

## MCP telemetry and protocol-era discipline

qyl classifies and stores MCP telemetry, owns the collector semantic catalog, and hosts MCP code of its own. The
2026-07-28 protocol revision changes what several MCP fields mean; these rules bind the ingest and enrichment path and
qyl's MCP host:

- Protocol era is the negotiated protocol version, never the presence of a
  `_meta` envelope — the legacy-fallback probe also carries one.
- MCP client and server identity is per-request and self-reported. Never promote `clientInfo` / `serverInfo` to a
  telemetry resource attribute, a routing dimension, or a behavior or security decision — display, logging, and
  debugging only.
- A multi-round tool call is N linked requests correlated by an opaque, untrusted `requestState`. Render the rounds as
  linked spans, never a synthesized parent-child tree, and trust `requestState` only after verification.
- Span and RPC status come from the JSON-RPC and tool outcome, never the HTTP status: a modern-path JSON-RPC error rides
  HTTP 400, and an error can arrive in-band on a committed 200. Map from the tool `isError` result and the protocol
  error-code family.
- Wire concepts upstream semconv has not defined — `requestState`, round index, `resultType`, `subscriptions/listen`
  lifetime, cache hints — enter the collector semantic catalog under the experimental `qyl.mcp.*` staging namespace,
  deletion-targeted on every semconv bump that lands an upstream equivalent. Never mint an `mcp.*` alias for an
  unratified concept.

## Verification

Run the narrow tests for the changed component and finish repository-wide work with:

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

The `Ci` target builds and tests the backend, builds and tests the product dashboard, runs its embedded Release-product
Playwright smoke, verifies the exact generated contract package, and checks the collector semantic catalog. For
schema-boundary changes, also compile and test the owning `qyl-api-schema`
repository and restore the resulting `Qyl.Api.Contracts` package into a clean consumer.

## Durable references

- Product and local development: `README.md`
- API authority: `https://github.com/ANcpLua/qyl-api-schema`
- Automatic instrumentation evidence:
  `https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation`
- Semantic-convention generation:
  `https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions`

When a claim can be derived from a manifest, generated report, public API baseline, or test, link that evidence instead
of copying it into another Markdown ledger.
