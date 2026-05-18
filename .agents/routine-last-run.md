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

## qyl-functional-tests 2026-05-18 00:44

**Outcome:** second feature landed + latent parallel-test race fixed. PR #351.

**State on arrival:**
- `.NET 10.0.300` SDK present at `~/.dotnet/` (the 2026-05-17 dotnet-missing
  blocker is resolved on this workstation).
- `dotnet build qyl.slnx` — 0 errors. Clean baseline.
- One functional test class on `main` (`HealthUiEndpointTests` from PR #343).
  Its three `[Fact]`s share one `IClassFixture` and run sequentially, so the
  in-process DuckDB migration race never surfaced.
- Top of `main`: `c1dfe301 chore(agents): record routine run blocker (#349)`.

**Changes shipped (PR #351 — 2 commits):**
- `32ee34e3 refactor(testability/functional): serialize parallel test classes via Collection`
  - Added `tests/qyl.collector.tests/Functional/FunctionalCollection.cs`
    (`[CollectionDefinition("Functional", DisableParallelization = true)]`).
  - Pulled `HealthUiEndpointTests` into the same collection (one-line touch).
- `2f660e75 test(functional/observe): cover subscription endpoints end-to-end`
  - `tests/qyl.collector.tests/Functional/ObserveSubscriptionEndpointsTests.cs`
    — 8 `[Fact]`s covering `/api/v1/observe/{catalog,/,{id}}`: catalog shape,
    three validation 400s, schema-mismatch 409, schema-drift 200+warning,
    POST→GET→DELETE round-trip, DELETE-unknown 404.
  - Nested `CollectorFactory : WebApplicationFactory<Program>` mirrors the
    pattern from `HealthUiEndpointTests`. Named in-memory DuckDB
    `":memory:qyl-obs-<guid>"` per fixture for catalog isolation.

**Substitutions:**
- DuckDB → `:memory:<guid>` (same as Health's pattern but with a unique name
  to dodge the shared-catalog race). `QYL_OTLP_AUTH_MODE=Unsecured` for clean
  boot. No outbound HTTP, no FakeTimeProvider, no WireMock needed for this
  feature.

**The parallel-execution race:**
- Adding a second functional test class surfaced
  `DuckDB.NET.Data.DuckDBException : TransactionContext Error: Catalog
  write-write conflict on alter` on roughly every other run. Two fixtures'
  migration runs overlap because DuckDB takes a process-wide lock on catalog
  ALTER even between distinct named in-memory databases. Named in-memory DBs
  alone don't fix it; the `[Collection]` serialization does.
- Verified by stress: 8/8 consecutive full-suite runs green after the
  refactor (20 tests total, excluding `Category=regen`).

**Verification:**
- `dotnet build qyl.slnx` — 0 errors.
- `Qyl.Collector.Tests` (regen excluded) — 20/20 across 8 consecutive runs.
- `ObserveSubscriptionEndpointsTests` standalone — 8/8 in ~0.5s.
- One iteration of the schema-version-rejection assertion was wrong on the
  first author pass (sent `999.0.0` instead of `semconv-999.0.0` — fell into
  `SemconvVersionParser.TryParse` → false → permissive Accept). Fixed before
  commit; documented inline so the next routine knows the parser's prefix
  requirement.

**Gaps for the next run:**
- **Mutation testing** — Stryker.NET is globally installed
  (`~/.dotnet/tools/dotnet-stryker`) and was not run this iteration. Scope
  for the next run: `services/qyl.collector/Observe/{ObserveEndpoints,
  SubscriptionManager,ObserveCatalog,SchemaVersionNegotiator}.cs` against
  `ObserveSubscriptionEndpointsTests`.
- **Next feature gap** — `/api/v1/configurator/*` (`ProvisioningEndpoints`)
  has rich validation branches and a `GenerationProfileService` collaborator.
  Either extract `IGenerationProfileService` (refactor commit, then test
  commit), or accept the same in-memory-DuckDB boot pattern. `/api/v1/schema/promotions` (`SchemaEndpoints`) has a similar shape.
- **Middleware contracts** — `UseQylCollectorMiddleware` is exercised
  end-to-end by every WebApplicationFactory test but no test asserts
  redaction / exception-capture / tracing emission shape through the
  pipeline. Worth a dedicated functional test class.
- **`qyl.mcp` service** — still no functional coverage. Different host
  shape (MCP stdio + HTTP), needs its own factory.
- **Nuke `FunctionalTests` target** — SKILL.md sketches it, not added this
  run to stay scoped. Could ride the next feature commit.

**Handoff:** none. PR #351 is open and awaits review/merge; do not push to
`main` from this routine.
