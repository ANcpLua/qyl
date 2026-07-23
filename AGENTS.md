# qyl engineering contract

This is the only editable agent/contributor instruction file in this repository.
`CLAUDE.md` is a symlink to it. Keep public explanation in `README.md`, released
history in release notes, and executable truth in code, schemas, generators, and
tests. Do not add progress diaries, repair prompts, handoff documents, or a second
rules file.

## Product and delivery

qyl is the collector, storage, investigation API, dashboard, and local host for an
OTLP-native, DuckDB-backed traces-and-logs product. Trace and log signals have full
ingest, storage, query, and dashboard verticals. Metrics are accepted only at the
standard OTLP wire endpoints, counted, discarded, and acknowledged with
`partial_success`; other OTLP signals have no endpoint or service. It is beta.
Unpublished Qyl surfaces may converge directly; published package versions are
immutable and move through new versions rather than compatibility shims without a
proven consumer.

Work directly on `main`, preserve unrelated user changes, run the repository gates,
make one intentional commit per coherent repository change, and push it. Generated
files are changed through their schema or generator and regenerated in the same
commit.

## Contract ownership

There is one owner for each boundary:

- **Qyl product API:** every client-visible request, response, stream event, and
  error is defined in the sibling `qyl-api-schema` TypeSpec repository and consumed
  through `Qyl.Api.Contracts` or a generated client. Do not declare parallel public
  DTOs in `qyl.collector`, `Qyl.Host`, the dashboard, or MCP code.
- **OTLP ingestion:** the wire contract is the official OpenTelemetry protobuf
  schema. Vendoring a pinned upstream `.proto` input is allowed; redefining it as a
  Qyl-owned DTO hierarchy is not.
- **Runtime internals:** storage rows, ingest batches, query models, and projections
  may be owned locally, but must not cross an HTTP, gRPC, MCP, streaming, or
  generated-client boundary.

Map explicitly between these domains. If an internal shape must cross a boundary,
change TypeSpec first, regenerate `Qyl.Api.Contracts` and client artifacts, then map
to the generated contract. Accessibility modifiers do not decide contract status:
anything serialized across a boundary is a contract.

## Implementation rules

- A public capability needs an executable owner: a product call path, an owned
  downstream consumer, or a conformance application exercising the complete
  contract. Mock-only tests and imaginary consumers are not acceptance evidence.
- Reuse a released, AOT-compatible upstream implementation when it satisfies the
  contract. Implement a missing gap only when Qyl needs it and prove it through a
  complete executable vertical.
- Tests and fixtures use real protocol types, valid programmatically generated data,
  or captured-and-sanitized datasets. Do not claim interoperability from hand-shaped
  JSON, substring checks over binary payloads, or mocks that merely echo inputs.
- `Version.props` owns the Qyl product version and the shared package-version
  properties; `Directory.Packages.props` owns the central `PackageVersion` entries
  that consume them. Do not hardcode package or banner versions elsewhere.
- The toolchain is the `global.json` SDK (`10.0.302`, `latestFeature`) and C# 14.
  Interceptors are supported on this SDK; use the current Roslyn APIs.
- Native AOT is the collector's publish contract (`QylAot` defaults on; the
  Dockerfile publishes the native lane). `eng/scripts/collector-aot-smoke.sh` is
  that lane's executable owner; `-p:QylAot=false` is the JIT diagnostic build with
  full analyzer enforcement.
- HTTP header attributes are denied at the collector persistence boundary unless
  the exact span key is in the small generated safe-header allowlist. Never persist
  `Authorization`, cookies, `Mcp-Param-*`, or arbitrary tool-defined header names
  in spans, logs, resources, entities, fixtures, or exception evidence.
- Never hand-edit generated C#, protobuf output, TypeScript contracts, or generated
  reports. Analyzer release manifests are maintained inputs and change with their
  analyzer rules.

## MCP telemetry and protocol-era discipline

qyl classifies and stores MCP telemetry, owns the collector semantic catalog, and
hosts MCP code of its own. The 2026-07-28 revision changes what several MCP fields
mean; these rules bind the ingest and enrichment path and qyl's MCP host.

- Protocol era is the negotiated protocol version, never the presence of a `_meta`
  envelope: the legacy-fallback probe also carries one. Classify and tag era from
  the negotiated version alone.
- MCP client and server identity is per-request and self-reported. Do not derive it
  from a session-scoped accessor, and never promote `clientInfo` / `serverInfo` to a
  telemetry resource attribute, a routing dimension, or a behavior or security
  decision — they are display, logging, and debugging values only.
- A multi-round tool call is N linked requests correlated by an opaque, untrusted
  `requestState`. Render and correlate the rounds as linked spans, never a
  synthesized parent-child tree, and trust `requestState` only after verification.
- Span and RPC status come from the JSON-RPC and tool outcome, never the HTTP
  status: a modern-path JSON-RPC error rides HTTP 400, and an error can arrive
  in-band on a committed 200. Map from the tool `isError` result and the protocol
  error-code family.
- Wire concepts OpenTelemetry semconv has not defined — `requestState`, round index,
  `resultType`, `subscriptions/listen` lifetime, cache hints — enter the collector
  semantic catalog under an experimental `qyl.mcp.*` staging namespace,
  deletion-targeted on every semconv bump that lands an upstream equivalent. Never
  mint an `mcp.*` alias for an unratified concept.

## Verification

Run the narrow tests for the changed component and finish repository-wide work with:

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

The `Ci` target builds and tests the backend, builds and tests the product
dashboard, runs its embedded Release-product Playwright smoke, verifies the exact
generated contract package, and checks the collector semantic catalog. For
schema-boundary changes, also compile and test the owning `qyl-api-schema`
repository and restore the resulting `Qyl.Api.Contracts` package into a clean Qyl
consumer.

## Durable references

- Product and local development: `README.md`
- API authority: `https://github.com/ANcpLua/qyl-api-schema`
- Automatic instrumentation evidence:
  `https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation`
- Semantic-convention generation:
  `https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions`

When a claim can be derived from a manifest, generated report, public API baseline,
or test, link that evidence instead of copying it into another Markdown ledger.
