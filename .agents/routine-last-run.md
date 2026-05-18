# Routine last-run log

Lightweight breadcrumb for scheduled agent routines. Each entry under
`## <routine-name> YYYY-MM-DD HH:MM` records the state the routine exited in,
so the next run can resume from the same line of work without re-discovering it.

## qyl-functional-tests 2026-05-17 02:27

**Outcome:** bootstrap + first feature test landed.

**State on arrival:**
- No functional-test scaffolding existed. `tests/qyl.collector.tests` already
  referenced `Microsoft.AspNetCore.Mvc.Testing` (presumably anticipated by a
  prior session) but no `WebApplicationFactory<Program>` usage.
- `tests/qyl.mcp.tests` had no in-process hosting set up either; skipped for
  this run (collector is the bigger surface).
- Nuke had `UnitTests` and `IntegrationTests` namespace-filtered targets but
  no `FunctionalTests` target. Both unit/integration filters currently match
  zero tests — namespaces aren't in use yet.
- `services/qyl.collector/Program.cs` was missing the
  `public partial class Program;` declaration required by
  `WebApplicationFactory<TEntryPoint>`.

**Changes shipped:**
- `services/qyl.collector/Program.cs` — added `public partial class Program;`
  trailing the top-level statements.
- `eng/build/BuildTest.cs` — added `FunctionalTests` Nuke target mirroring
  `UnitTests` / `IntegrationTests`, filtering on `*.Functional.*` namespace.
- `tests/qyl.collector.tests/Functional/HealthUiEndpointTests.cs` — first
  functional test: `GET /health/ui` happy path + auth-allowlist confirmation.
  Booted full collector host in-process via `CollectorFactory :
  WebApplicationFactory<Program>`; overrode `QYL_DATA_PATH=:memory:` and
  `QYL_OTLP_AUTH_MODE=Unsecured` through `ConfigureAppConfiguration`.

**Substitutions:**
- DuckDB → `:memory:` (in-process, same engine, no file on disk). The routine
  prescribes SQLite in-memory or a fake repo; `DuckDbStore` has no interface
  to fake without an invasive production refactor, so `:memory:` DuckDB is the
  closest in-process analog. Real file-backed DuckDB / concurrency / recovery
  behaviour belongs in `qyl-integration-tests`, not here.
- No outbound HTTP is hit on the `/health/ui` path, so WireMock wasn't
  required this run. Will be needed for the next feature (any endpoint that
  calls a downstream service).

**Verification:**
- `nuke FunctionalTests --skip Compile` → 2/2 pass in ~1 s (after rebuilding
  `qyl.mcp.tests` — `dotnet test`'s pre-flight check rejects unbuilt MTP
  projects as VSTest until their MTP discovery files land on disk).
- Full collector test suite (`--filter-not-trait Category=regen`) →
  11/11 pass; no regressions to the 9 pre-existing tests.

**Gaps for the next run:**
- WireMock package not yet in `Directory.Packages.props`. Add when the next
  feature picked actually calls an external HTTP service (don't add
  speculatively).
- `FakeTimeProvider` (Microsoft.Extensions.Time.Testing) not yet in central
  packages — needed for any endpoint whose behaviour is time-driven
  (background flushers, scheduled emissions).
- Stryker.NET is not configured in this repo at all. Mutation testing on
  production code touched is part of the routine but can't run without
  Stryker config. Recommend wiring up in a dedicated config commit before
  next run, scoped to `services/qyl.collector` and an explicit file list.
- `qyl.mcp` service has no functional-test coverage yet. `services/qyl.mcp`
  uses a different host shape (MCP stdio + HTTP) — needs its own factory.
- Endpoints with high blast radius and zero functional coverage:
  `/v1/traces`, `/v1/logs` (OTLP ingestion), all `Autofix/*Endpoints.cs`,
  `Workflows/WorkflowEndpoints.cs`, `Insights/InsightsEndpoints.cs`.
- Middleware (`UseQylCollectorMiddleware`) is exercised end-to-end by every
  test that uses `WebApplicationFactory`, but no test asserts middleware
  contracts directly (redaction, exception capture, auth gate behaviour).

**Handoff:** none. No work that belongs in `qyl-unit-tests`,
`qyl-integration-tests`, or `qyl-e2e-tests` was deferred from this run.

## qyl-e2e-tests 2026-05-18 03:50

**Outcome:** first real E2E scenario landed on top of the bootstrap topology.
Three logical commits on `tests/auto-e2e-2026-05-18`: Dockerfile bumps,
fixture testability fix, scenario test.

**State on arrival:**
- Last `tests/auto-*` branch was `tests/auto-functional-2026-05-18`.
- `tests/qyl.e2e.tests` was already bootstrapped (#347) but only the
  `WireMockLlmSeamTests` (`Category=E2EBootstrap`, no Docker) had ever
  executed; the `QylTopologyFixture` had never been booted end-to-end.
- `nuke Ci` not run on arrival (heavy) — full solution build via
  `dotnet build qyl.slnx` finishes green with 0 errors / 1407 warnings
  (analyzer noise, baseline-consistent).
- Docker present (OrbStack, 3.88 GB total). dotnet 10.0.300 on PATH.
- Latest commit on main: `c1dfe301` (chore(agents): record routine run
  blocker — dotnet not installed in container).

**Changes shipped (three commits on `tests/auto-e2e-2026-05-18`):**

1. `fix(docker): bump .NET base images so SDK 10.0.300 actually builds`
   — `services/qyl.collector/Dockerfile`, `services/qyl.loom/Dockerfile`,
   `services/qyl.mcp/Dockerfile`. Pinned SHAs in all three Dockerfiles
   were resolving to SDK 10.0.203 while `global.json` requires
   10.0.300 (regression from #346). Every `nuke DockerImageBuild`
   broken since #346 — no CI job runs `DockerImageBuild`, so the
   defect was latent. Bumped sdk:10.0, aspnet:10.0, sdk:10.0-alpine,
   runtime-deps:10.0-alpine to latest SHAs. Also pinned MCP runtime
   stage to `--platform=linux/amd64` so the cross-compiled
   `linux-musl-x64` binary matches the runtime image arch on
   arm64 dev hosts (previously crashed with
   `ld-musl-x86_64.so.1 not found`).

2. `refactor(tests/e2e): make topology fixture actually boot
   collector + mcp` — `tests/qyl.e2e.tests/Topology/QylTopologyFixture.cs`.
   Two latent defects in the bootstrap fixture: (a) collector aborts on
   startup in default `Production` env because `QYL_OTLP_AUTH_MODE`
   defaults to `ApiKey` with no key configured — fixed by passing
   `QYL_OTLP_AUTH_MODE=Unsecured`; (b) MCP Dockerfile sets
   `ASPNETCORE_URLS=http://+:8080` so nothing answered on 5200 —
   fixed by overriding `ASPNETCORE_URLS` to 5200 and waiting on the
   real `/alive` health endpoint instead of the log message. Also
   collapsed the dual-ctor pattern into a single public constructor
   (xUnit v3 `ICollectionFixture<T>` requires exactly one).

3. `test(e2e/otlp-ingest): cover OTLP/HTTP JSON ingest reaching DuckDB
   storage` — `tests/qyl.e2e.tests/Scenarios/OtlpHttpTraceIngestionRoundtripTests.cs`.
   POST a single-span OTLP/HTTP JSON payload to the running collector
   container, expect 202, then poll `/api/v1/telemetry/stats` (bounded
   15s, 250ms cadence) until `spanCount` increases past the pre-ingest
   baseline. Exercises HTTP receiver -> JSON parse -> OtlpConverter ->
   SpanRingBuffer.PushRange -> DuckDbStore.EnqueueAsync ->
   GetStorageStatsAsync. Test class joins `E2ECollection` via
   `[Collection(E2ECollection.Name)]` so future scenarios share the
   topology.

**Verification:**
- `dotnet build qyl.slnx` — 0 errors, 1407 warnings.
- `docker build` for both collector and mcp — succeed.
- `dotnet test --filter-trait Category=E2EBootstrap` — green (~700ms).
- `dotnet test --filter-trait Category=E2E` — green 4 consecutive runs
  (~5-7s per run, including container start/stop). Zero flakes.
- `nuke E2ETests` Nuke target wired and wired to `DockerImageBuild`,
  but not executed this run (would rebuild qyl-loom + qyl-dashboard
  too, both out of scope here).

**Bug surfaced for follow-up:** the read side of `/api/v1/traces`
(`GetSpansAsync` -> `SpanStorageRow.MapFromReader`) is fully broken in
production. Schema declares `spans.kind VARCHAR NOT NULL` and
`spans.status_code VARCHAR NOT NULL` (DuckDbSchema.g.sql:316,319),
but `SpanStorageRow.Kind`/`StatusCode` are `byte`. Source generator at
`internal/qyl.collector.storage.generators/DuckDbEmitter.cs:219-222`
emits `reader.Col(N).GetByte(0)`, throwing
`InvalidCastException: System.String -> System.Byte` on every span
row. Repro: `POST /v1/traces`, then `GET /api/v1/traces` — returns
500. Fix is either schema migration to TINYINT or property type
change to string. NOT fixed here — separate concern from "add E2E
test" and would need its own focused PR with migration testing. The
scenario test's class-level XML comment records the repro so the gap
stays discoverable.

**Gaps for the next run:**
1. **Fix the spans VARCHAR-vs-byte read-mapping bug** (highest
   priority — this is a real production data-fetch defect, not an
   E2E concern). Once fixed, extend this scenario to also assert the
   trace is queryable via `GET /api/v1/traces/{traceId}` (richer
   roundtrip).
2. **Second E2E scenario: MCP -> collector handshake**. The MCP
   container is now booted and queryable; drive an MCP tool that
   reads collector data (POST OTLP first, then invoke an MCP search
   tool over JSON-RPC, assert the span is returned).
3. **Third E2E scenario: chat ingest -> trace at sink with
   credentials redacted** (the original handoff candidate). Needs a
   sink container (OTel collector with file exporter) added to the
   fixture plus LLM wiring on qyl.loom or a stub MCP server. Larger
   surface; do after #1 and #2.

**Handoff:** PR on `tests/auto-e2e-2026-05-18`. Do not merge — review
and follow-up bug fix first.

## qyl-e2e-tests 2026-05-17 02:45

**Outcome:** bootstrap PR opened.

First-ever run of `qyl-e2e-tests` on a workstation with the full toolchain
present (dotnet 10.0.300, Docker 29.4.0). The previous run (2026-05-16)
exited as a no-op because the remote container lacked `dotnet`.

Per the skill's bootstrap section, this run produced the **infrastructure-only
PR** (project + topology fixture + Nuke target + central-package pins). **No
scenario tests** were added; the next routine run picks up from here and adds
the first scenario.

**Changes shipped:**
- `tests/qyl.e2e.tests/qyl.e2e.tests.csproj` — new test project, `ANcpLua.NET.Sdk.Test`.
- `tests/qyl.e2e.tests/E2ECollection.cs` — `[CollectionDefinition("E2E", DisableParallelization = true)]`.
- `tests/qyl.e2e.tests/Topology/QylTopologyOptions.cs` — image tags + startup timeout.
- `tests/qyl.e2e.tests/Topology/QylTopologyFixture.cs` — programmatic Testcontainers
  topology: bridge network → `qyl-collector:latest` → `qyl-mcp:latest`; WireMock
  process-local; containers reach it via `host.docker.internal`.
  `WithImagePullPolicy(_ => false)` enforces local-image-only.
- `tests/qyl.e2e.tests/Bootstrap/WireMockLlmSeamTests.cs` — `Category=E2EBootstrap`
  smoke (no Docker) — proves the WireMock seam roundtrips a scripted
  `/v1/chat/completions` and shows up in `LogEntries`.
- `eng/build/BuildTest.cs` — new `E2ETests` target depending on
  `IDocker.DockerImageBuild`, filters `Category=E2E`. Default `Test` excludes
  `Category=E2E` (bootstrap tests stay in).
- `qyl.slnx` — registered the project.
- `Version.props` + `Directory.Packages.props` — added `WireMock.Net 2.6.0` and
  `Testcontainers 4.11.0`. Split-pinned `OpenTelemetry.Instrumentation.AspNetCore`
  to 1.15.2 (forced by WireMock.Net 2.6.0 transitive); Http + Runtime stay at
  the umbrella 1.15.1 (no 1.15.2 release exists for them).

**Verification:**
- `dotnet build qyl.slnx` — 0 errors, 1454 warnings (baseline 1393; ~61 new
  warnings are all `MultipleGlobalAnalyzerKeys` from the dual-`.globalconfig`
  worktree setup — benign, present on every worktree build).
- `dotnet test tests/qyl.e2e.tests --filter-trait Category=E2EBootstrap` — 3
  consecutive runs, all green (~1s each, 0 flakes).
- `nuke E2ETests` target wired up but **not executed** this run (would require
  rebuilding all four qyl Docker images — out of scope for the bootstrap PR).

**Overlap with `Smoke`:** `eng/smoke/run.sh` (Nuke target `Smoke`) is the
PRD #173 quality gate using real Ollama + real qyl Compose stack. E2E uses
**WireMock** for deterministic LLM stubbing. The two don't overlap — Smoke
covers "does the stack work against a real model"; E2E covers "does the
stack route data correctly given a known-bad/redacted LLM response".

**Gaps for the next run:** add the first real scenario. Highest-value candidates:
1. Agent submits chat → trace arrives at downstream sink with credentials redacted.
2. MCP HTTP session reconnect after transient collector failure.
3. Cost rollup updates after a single chat completion.

Pick exactly one. Add a sink container (e.g. an OTel collector configured to
write to a file volume) to `QylTopologyFixture` for scenario 1.

**Handoff:** bootstrap PR opened; next cycle picks up from here.

## qyl-test-push 2026-05-17

**Outcome:** BLOCKED — dotnet not installed in container. Run stopped without changes.

**Blocker:**
`dotnet` is absent from the container PATH and is not part of this environment's
required toolchain (Docker, Rust, Go, Java, and others are all present via
`check-tools`; .NET is not). `which dotnet` and a full `find /` returned nothing.
This is the same hard stop that halted the 2026-05-16 run.

**State on arrival:**
- Build was not attempted — cannot call `nuke Ci` or `dotnet build` without the SDK.
- Latest commit: `6f8afc0 feat(ci): add native auto-merge workflow (#348)`.
- No `tests/auto-*` branch exists yet for 2026-05-17.
- Last functional-test run (2026-05-17 02:27) shipped `HealthUiEndpointTests`;
  last e2e run (2026-05-17 02:45) shipped the topology bootstrap.

**What the next run should do (when dotnet is available):**
1. Run `nuke Ci` to confirm green baseline.
2. Pick ONE gap from the prior run's gap list (highest priority: OTLP ingestion
   endpoint `/v1/traces` or `/v1/logs` has zero functional coverage).
3. Bootstrap Stryker.NET config for `services/qyl.collector` (noted missing since
   02:27 run; no mutation testing is possible without it).
4. For e2e: add the first real scenario — agent chat → trace arrives at sink with
   credentials redacted. Requires adding a sink container to `QylTopologyFixture`.

**No code was changed this run.**
