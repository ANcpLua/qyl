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
