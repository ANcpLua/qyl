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
