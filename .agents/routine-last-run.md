# Routine last-run log

Lightweight breadcrumb for scheduled agent routines. Each entry under
`## <routine-name> YYYY-MM-DD HH:MM` records the state the routine exited in,
so the next run can resume from the same line of work without re-discovering it.

## qyl-functional-tests 2026-05-20

**Outcome:** SchemaEndpoints functional coverage landed + production bug
fix surfaced in the same PR (#360, `tests/auto-functional-2026-05-20`).
Two commits: storage-layer fix, then the eight-row functional class.

**State on arrival:**
- Worktree clean on `claude/gifted-einstein-589b33`, `main` HEAD =
  `82c7ad63 refactor(qyl.mcp): delete dead ServiceProviderRef plumbing`.
- `dotnet build qyl.slnx` — 0 errors, 1582 warnings (baseline analyzer
  noise, consistent with previous routine runs).
- Two functional test classes on main from PRs #343 + #351
  (`HealthUiEndpointTests`, `ObserveSubscriptionEndpointsTests`); both
  still green this run.
- `eng/build/BuildTest.cs` already has the `FunctionalTests` Nuke target
  (`*.Functional.*` namespace filter) from PR #343.
- `.NET 10.0.300` on PATH at `~/.dotnet/`; Stryker.NET globally installed
  at `~/.dotnet/tools/dotnet-stryker` but the repo still has no
  `stryker-config.json` — same blocker the 2026-05-18 run flagged.

**Changes shipped (PR #360 — two commits on `tests/auto-functional-2026-05-20`):**

1. `542a765c fix(schema-control): persist sql_statements so promotions can actually apply`
    - `services/qyl.collector/Storage/DuckDbSchema.SchemaControl.cs` —
      added `sql_statements VARCHAR NOT NULL DEFAULT ''` to the
      `schema_promotions` CREATE TABLE.
    - `services/qyl.collector/Storage/DuckDbStore.SchemaControl.cs` —
      extended `InsertSchemaPromotionAsync` to bind `record.SqlStatements`
      as `$10`; extended both `SELECT` statements in
      `GetSchemaPromotionAsync` / `GetSchemaPromotionsByStatusAsync` to
      project `sql_statements` at column index 9; mapped it through
      `MapSchemaPromotion` (previously hard-coded to `string.Empty`).
    - **What was broken:** `SchemaPlanner.PlanPromotionAsync` generates
      DDL into `SchemaPromotionRecord.SqlStatements`, but the table had
      no column for it — INSERT dropped the SQL, READ rebuilt the record
      with `SqlStatements = ""`, and `SchemaExecutor.ExecutePromotionAsync`
      then called `ExecuteSchemaDdlAsync("")` which throws inside
      DuckDB.NET's `PrepareMultiple`. Every apply call landed in
      `status='failed'`; `/api/v1/schema/promotions/{id}/apply` had
      never actually worked since the feature shipped.
    - Zero non-test callers of `SchemaPlanner` / `SchemaExecutor` outside
      the four HTTP routes, so the blast radius is contained.

2. `29c56e2b test(functional/schema-control): cover /api/v1/schema/promotions/* end-to-end`
    - `tests/qyl.collector.tests/Functional/SchemaPromotionEndpointsTests.cs`
      — eight `[Fact]` rows covering:
      validation-400 for missing `TargetTable` / `ChangeType`; happy-path
      POST returns 201 with `Created` Location header and the planner
      record (camelCase via `JsonSerializerDefaults.Web` reflection
      fallback — `SchemaPromotionRecord` is not in
      `QylSerializerContext`); GET-by-id returns the persisted row with
      `sql_statements` round-tripping through DuckDB (the read-path
      regression guard for the fix above); GET-by-unknown-id → 404;
      GET-list (bare array, not envelope) includes the new promotion;
      POST apply actually executes the DDL against the in-memory
      catalog, `status` flips to `'applied'`, `appliedAt` is non-null,
      and the row drops out of the pending list; POST apply for an
      unknown id → 404.
    - `CollectorFactory : WebApplicationFactory<Program>` mirrors the
      ObserveSubscriptionEndpointsTests pattern: named in-memory DuckDB
      `:memory:qyl-schema-<guid>` per fixture, `QYL_OTLP_AUTH_MODE=Unsecured`
      for clean boot. Joined to `FunctionalCollection` so migration runs
      stay serial across functional fixtures.

**Substitutions:**
- DuckDB → `:memory:qyl-schema-<guid>` (real engine, isolated per fixture,
  no file on disk). The `apply` test's DDL executes against this catalog.
- No outbound HTTP → no WireMock / `TUnit.Mocks.Http` needed this run.
- No time-driven behavior → no `FakeTimeProvider` needed.

**Verification:**
- `dotnet build qyl.slnx` — 0 errors, 166 warnings (analyzer baseline).
- `dotnet run --project tests/qyl.collector.tests --
  -class "Qyl.Collector.Tests.Functional.SchemaControl.SchemaPromotionEndpointsTests"`
  — 8/8 across 3 consecutive runs, ~0.5s each. Zero flakes.
- Full collector test suite (excluding `Category=regen`, which intentionally
  fails when the working tree has uncommitted edits) — 28/28 green; the
  two pre-existing functional classes still pass.

**xUnit → TUnit migration:** not done this run. The SKILL.md sketches
TUnit.AspNetCore but the project is xUnit v3 + `WebApplicationFactory<Program>`,
and the global CLAUDE.md is explicit about following existing patterns.
A migration belongs in its own dedicated commit, not bundled with feature
coverage; the routine itself warns "TUnit.AspNetCore / factory issues
you can't resolve in a few minutes → revert."

**Gaps for the next run:**
1. **Stand up `stryker-config.json`** scoped to `services/qyl.collector` so
   subsequent functional runs can satisfy the routine's mutation-testing
   step (`~/.dotnet/tools/dotnet-stryker` is installed but unconfigured).
2. **`/api/v1/configurator/*` (`Provisioning/ProvisioningEndpoints.cs`)**
   — rich validation branches + `GenerationProfileService` collaborator,
   next obvious 6–10-row functional class.
3. **Middleware contracts** — `UseQylCollectorMiddleware` is exercised
   by every fixture but no test asserts redaction / exception-capture /
   trace-emission shape through the pipeline.
4. **`qyl.mcp` service** — still zero functional coverage. Different host
   shape (MCP stdio + HTTP) → needs its own factory.
5. **`add_index` ChangeType** — this run covers `add_column` validation
   plus `add_table` happy/apply paths; an `add_index` test that pre-creates
   the table then promotes an index against it would round out planner
   coverage of all three allowed change types.
6. **GET-list ordering** — endpoint returns `created_at DESC` but no test
   asserts the order across multiple inserts.

**Handoff:** PR #360 is open and awaits review/merge. Do not push to
`main` from this routine.

## qyl-e2e-tests 2026-05-19 07:52

**Outcome:** BLOCKED — Docker daemon not running. Run stopped without changes.

**Blocker:**
OrbStack's Docker socket is missing
(`/Users/ancplua/.orbstack/run/docker.sock` does not exist). `docker info`
errors with `dial unix /Users/ancplua/.orbstack/run/docker.sock: connect: no
such file or directory`. The OrbStack app is installed
(`/Applications/OrbStack.app` present, `~/.orbstack/bin` on PATH) but the
daemon is not running — `pgrep -fl -i orbstack` returns no OrbStack
processes (only unrelated MCP helpers that happen to have `~/.orbstack/bin`
on their inherited PATH).

The `QylTopologyFixture` boots `qyl-collector` + `qyl-mcp` containers via
Testcontainers; without a Docker daemon the E2E routine cannot do its single
useful thing. The skill explicitly lists this as a documented no-op:
*"Docker not available → note + exit (most common no-op)."* This run does
not attempt to start OrbStack autonomously — bringing a desktop daemon /
VM up is the user's call, not the scheduled task's.

**State on arrival:**
- Worktree clean on `claude/dreamy-germain-432280`; `origin/main` HEAD is
  `c2ab2116 chore(nuget): add GitHub Packages feed for O-ANcppLua org`.
- `dotnet build qyl.slnx` not attempted (a build doesn't unblock anything
  when the containers can't boot).
- Last E2E run shipped `OtlpHttpTraceIngestionRoundtripTests` on PR #353
  (`tests/auto-e2e-2026-05-18`).
- Test inventory unchanged:
  `tests/qyl.{collector,collector.integration,e2e,mcp}.tests`.

**Carry-forward gaps (unchanged from 2026-05-18):**
1. **Production bug, highest priority:** `spans.kind` / `spans.status_code`
   declared `VARCHAR NOT NULL` in `DuckDbSchema.g.sql:316,319`, but
   `SpanStorageRow.Kind` / `.StatusCode` are `byte`. The source generator at
   `internal/qyl.collector.storage.generators/DuckDbEmitter.cs:219-222`
   emits `reader.Col(N).GetByte(0)`, so every `GET /api/v1/traces` row read
   throws `InvalidCastException`. Not an E2E concern — belongs in a focused
   PR with its own migration testing. Once fixed, extend
   `OtlpHttpTraceIngestionRoundtripTests` to also assert the trace is
   queryable via `GET /api/v1/traces/{traceId}`.
2. **Second E2E scenario:** MCP → collector handshake. Drive an MCP tool
   over JSON-RPC that reads spans previously ingested via OTLP/HTTP, assert
   the round-trip.
3. **Third E2E scenario:** chat ingest → trace at sink with credentials
   redacted (the original handoff candidate). Add an OTel collector sink
   container with a file exporter to `QylTopologyFixture`.

**No code changes this run.** This entry exists only as a breadcrumb.

**Handoff:** none.

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
