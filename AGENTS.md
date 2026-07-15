# qyl engineering contract

This is the only editable agent/contributor instruction file in this repository.
`CLAUDE.md` is a symlink to it. Keep public explanation in `README.md`, released
history in release notes, and executable truth in code, schemas, generators, and
tests. Do not add progress diaries, repair prompts, handoff documents, or a second
rules file.

## Product and delivery

qyl is the collector, storage, investigation API, dashboard, and local host for an
OpenTelemetry-compatible observability product. The collector ingests all four OTLP
signals (traces, logs, metrics, profiles) and owns the GenAI cost subsystem:
provider billing synchronization, the live model-pricing catalog, and the GenAI ETL
audit. It is pre-beta. Unpublished Qyl surfaces may converge directly; published
package versions are immutable and move through new versions rather than
compatibility shims without a proven consumer.

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

## External API services

- **Model-price catalog:** OpenRouter is Qyl's sole model-pricing authority. The
  collector refreshes
  [`GET https://openrouter.ai/api/v1/models?output_modalities=all`](https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties)
  to estimate observed model usage without maintaining provider-specific price
  tables. Its model-level `pricing` object is the lowest currently available
  OpenRouter rate, not a historical price, customer contract, routed-endpoint
  quote, or provider invoice. Preserve the source URL, retrieval time, immutable
  snapshot id, and `minimum_available_rate` semantics. Price fields such as
  `prompt`, `completion`, `request`, `image`, `web_search`,
  `internal_reasoning`, and `input_cache_read`/`input_cache_write` are decimal
  USD values per their documented token, request, image, search, audio, or other
  unit. Apply supported conditional overrides in source order and fail closed when
  required usage or a pricing condition is unavailable. `QYL_OPENROUTER_API_KEY`
  supplies the optional Bearer token. Do not add another model-price adapter or a
  configurable replacement endpoint without an explicit product-boundary change.
- **OpenRouter diagnostics:**
  [`GET /api/v1/models/{author}/{slug}/endpoints`](https://openrouter.ai/docs/api/api-reference/endpoints/list-all-endpoints-for-a-model)
  exposes provider-specific endpoint prices. It has a different response shape and
  is a reference for investigating catalog discrepancies; Qyl does not call it in
  the pricing path.
- **Actual provider billing:** OpenAI
  `GET https://api.openai.com/v1/organization/costs` and Anthropic
  `GET https://api.anthropic.com/v1/organizations/cost_report` reconcile billed
  organization or workspace spend. They stay separate from OpenRouter catalog
  estimates and must never be treated as interchangeable price sources.

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
- The toolchain is the `global.json` SDK (`10.0.301`, `latestFeature`) and C# 14.
  Interceptors are supported on this SDK; use the current Roslyn APIs.
- Native AOT is the collector's publish contract (`QylAot` defaults on; the
  Dockerfile publishes the native lane). `eng/scripts/collector-aot-smoke.sh` is
  that lane's executable owner; `-p:QylAot=false` is the JIT diagnostic build with
  full analyzer enforcement.
- Never hand-edit generated C#, protobuf output, TypeScript contracts, or generated
  reports. Analyzer release manifests are maintained inputs and change with their
  analyzer rules.

## Verification

Run the narrow tests for the changed component and finish repository-wide work with:

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

The `Ci` target builds and tests the backend, builds both first-party frontends,
runs Vitest and the embedded Release-product Playwright smoke for the dashboard
(the host console is build/typecheck only), checks that both
frontends consume one exact generated contract package, and verifies the collector
semantic catalog. For schema-boundary changes, also compile and test the owning
`qyl-api-schema` repository and restore the resulting `Qyl.Api.Contracts` package
into a clean Qyl consumer.

## Durable references

- Product and local development: `README.md`
- API authority: `https://github.com/ANcpLua/qyl-api-schema`
- Automatic instrumentation evidence:
  `https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation`
- Semantic-convention generation:
  `https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions`

When a claim can be derived from a manifest, generated report, public API baseline,
or test, link that evidence instead of copying it into another Markdown ledger.
