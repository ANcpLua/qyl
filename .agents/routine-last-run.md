# Routine last-run log

Lightweight breadcrumb for scheduled agent routines. Each entry under
`## <routine-name> YYYY-MM-DD HH:MM` records the state the routine exited in,
so the next run can resume from the same line of work without re-discovering it.

## qyl-e2e-tests 2026-05-24 09:46

**Outcome:** BLOCKED — Docker daemon still not running. Third consecutive
scheduled run of `qyl-e2e-tests` to hit this exact wall (2026-05-19,
2026-05-20, 2026-05-24; there was no scheduled e2e run on 05-21/22/23 — the
gap between 05-20 and 05-24 is the cron cadence, not me skipping). No code
touched.

**Blocker (identical to 2026-05-19 / 2026-05-20):** OrbStack's Docker socket
is still missing — `docker info` fails with `dial unix
/Users/ancplua/.orbstack/run/docker.sock: connect: no such file or directory`.
`/Applications/OrbStack.app` is installed but the daemon is not started.
Bringing the desktop daemon up is the user's call; this routine does not
launch GUI apps autonomously.

**State on arrival:**
- Worktree clean on `claude/clever-feynman-0c4209` (auto-generated isolation
  branch under `.claude/worktrees/`).
- `dotnet build` not attempted — a build cannot unblock a missing Docker
  daemon and the existing E2E tests would build green anyway (commit
  `0b08beba fix(tests/e2e): heal release-mode bit-rot…` landed since 05-20).
- E2E project on main currently ships **three** scenarios — one more than at
  the 2026-05-20 entry. New scenario landed via a non-routine commit:
  - `tests/qyl.e2e.tests/Bootstrap/WireMockLlmSeamTests.cs`
  - `tests/qyl.e2e.tests/Scenarios/OtlpHttpTraceIngestionRoundtripTests.cs`
  - `tests/qyl.e2e.tests/Scenarios/McpServerExposesCatalogTests.cs` ← **new**,
    from `83503c39 test(e2e/mcp): cover qyl-mcp's /llms.txt agent-discovery
    surface`. Closes carry-forward gap #2 from the 2026-05-20 entry (MCP
    catalog/discovery surface).
- CI investment also moved while this routine was blocked: `80c5b917 ci: gate
  docker e2e on relevant changes`, `5aead24b ci: keep docker e2e out of
  backend gate`, `3a1ea5a6 ci(e2e-docker): cache layers…`,
  `d56ea7e2 ci(e2e-docker): no-op nudge to benchmark warm GHA cache`. The
  E2E pipeline is being actively tuned by other PRs even though the
  workstation-side routine has been no-op for a week.

**Carry-forward gaps (revised — gap #2 was closed externally):**
1. **Priority-1 production bug — still present, verified today.**
   `services/qyl.collector/Storage/DuckDbSchema.g.sql:313,317` declares
   `kind VARCHAR NOT NULL` and `status_code VARCHAR NOT NULL`, but
   `internal/qyl.collector.storage.generators/DuckDbEmitter.cs:184,221`
   still emits `reader.Col(N).AsByte` / `reader.Col(N).GetByte(0)` for
   those columns. Every `GET /api/v1/traces` row read throws
   `InvalidCastException`. Same line numbers as the 2026-05-20 entry —
   the `527f9294 chore: emit DuckDbSchema.g.sql…` commit that touched
   the schema did not realign the generator. Not an E2E fix; needs its
   own focused PR with migration testing. Once landed, extend
   `OtlpHttpTraceIngestionRoundtripTests` to also assert
   `GET /api/v1/traces/{traceId}` returns the row.
2. **MCP → collector read-through scenario.** Drive an MCP tool over
   JSON-RPC that reads spans **previously ingested via OTLP/HTTP** (not
   the catalog/discovery surface that `McpServerExposesCatalogTests` now
   covers — that's a different seam). The round-trip assertion is the
   one with real production value, and it's gated on gap #1 above.
3. **Chat ingest → trace at sink with credential redaction.** Requires
   adding an OTel collector sink container with a file exporter to
   `QylTopologyFixture` (or migrating the fixture to TUnit.Aspire, which
   is the SKILL.md-prescribed direction but conflicts with the repo's
   xUnit-v3 reality — same TUnit-vs-xUnit conflict that
   `qyl-unit-tests` 2026-05-22 resolved by following CLAUDE.md). Keep
   xUnit; add the sink container; assert redaction at the sink.

**Pattern flag — escalated:**

The 2026-05-20 entry warned: "If this becomes three [consecutive Docker-down
no-ops], worth considering whether the routine should attempt a non-
interactive `open -gja OrbStack`." We're now at three. **I am still not
auto-launching OrbStack** — the reasoning from 2026-05-20 stands (agents
shouldn't auto-launch GUI apps without explicit instruction). But the
pattern is now a real signal worth raising:

- **User-side fix that would actually solve this:** add OrbStack to macOS
  Login Items (System Settings → General → Login Items → Open at Login).
  After that the daemon is up whenever the workstation is, and this routine
  stops being a perpetual no-op without anyone touching the schedule.
- **Routine-side change worth considering:** the cron entry could check
  `docker info` from a pre-task script and **skip the run entirely** (not
  even invoke the agent) when Docker is down, instead of paying the
  agent-spawn cost only to produce a no-op log entry like this one. That
  removes the per-day noise but loses the cross-channel observation value
  this entry just demonstrated (gap #2 closing was worth noticing). Net:
  leave the schedule alone; the cost of a no-op log entry is small.

**Handoff:** none. PR for this log entry only.

## qyl-functional-tests 2026-05-22 (auto)

**Outcome:** Added end-to-end functional coverage for `/api/v1/configurator/*`
(ProvisioningEndpoints) — the explicit next-up gap from PR #360's carry-forward
list. Adding the test surfaced two latent production bugs in the job-creation
path; both fixed in the same PR as a minimum, focused change.

**State on arrival:**
- Worktree clean on `claude/youthful-lamport-3ada0b`. `dotnet build qyl.slnx`
  clean (0 errors).
- Prior functional baseline: `HealthUiEndpointTests` (3 cases) + `Observe-
  SubscriptionEndpointsTests` (8 cases) = 11 functional cases on main, all
  green across three consecutive runs.
- Last functional branch on remote: `tests/auto-functional-2026-05-20` (PR
  #360, still OPEN — SchemaPromotionEndpoints coverage + a missing-column
  fix). My branch starts from main, not from that branch, so I do not see
  the SchemaPromotion class yet — it will land independently when #360
  merges.
- xUnit v3 + `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>`
  remains the repo's functional-test pattern. SKILL.md prescribes TUnit; the
  global CLAUDE.md says "follow existing architecture unless the task is to
  replace them," and CLAUDE.md wins on conflict. A TUnit migration belongs
  in its own dedicated commit, not bundled with a feature gap. Same
  rationale as PR #360.

**Changes shipped (two logical commits on `tests/auto-functional-2026-05-22`):**

1. `fix(provisioning): make /api/v1/configurator/jobs/* actually work
   end-to-end` — touches
   `services/qyl.collector/Storage/DuckDbStore.Provisioning.cs` (SQL column
   alignment to the actual `generation_jobs` schema) and
   `services/qyl.collector/Provisioning/ProvisioningEndpoints.cs`
   (nullable `workspaceId` on `GET /jobs`).
2. `test(functional/provisioning): cover /api/v1/configurator/* end-to-end`
   — adds `tests/qyl.collector.tests/Functional/Provisioning/Provisioning-
   EndpointsTests.cs` with 17 `[Fact]` rows covering all 7 routes:
   profiles list/get/404, selections validation/upsert/round-trip/404,
   jobs create/validation/list-ordering/cancel/double-cancel/404.

**Bugs surfaced + fixed:**

1. *DuckDB column drift.* `DuckDbStore.Provisioning.cs` was writing/reading
   `job_id` / `output_url` / `created_at` against a `generation_jobs` table
   whose live columns are `id` / `output_path` / `queued_at`. Plus it never
   bound the NOT NULL `job_type` and `priority` columns. Every
   `POST /api/v1/configurator/jobs` would have 500'd in production with
   `Binder Error: Table "generation_jobs" does not have a column with
   name "job_id"`. Aligned SQL to the actual `DuckDbSchema.g.cs:106-124`
   shape; `job_type` defaults to "full" and `priority` to 0 because the
   endpoint surface doesn't yet accept them.

2. *Minimal-API binder vs in-handler guard.* `GET /jobs` declared
   `string workspaceId` (non-nullable) which makes the route binder
   throw `BadHttpRequestException` before the in-handler
   `IsNullOrWhiteSpace` check runs; the Development exception middleware
   surfaced that as 500. The handler is already designed to return a
   clean 400 + JSON body — making the parameter nullable lets it.

**Fakes / stubs used:**

- `DuckDbStore` → `:memory:qyl-prov-<guid>` (named in-memory DB, unique per
  fixture). Same pattern as ObserveSubscriptionEndpointsTests and the
  SchemaPromotionEndpointsTests in PR #360. Avoids
  `Catalog write-write conflict on alter` collisions.
- `QYL_OTLP_AUTH_MODE=Unsecured` for clean boot. The configurator routes
  are not behind OTLP token auth.
- No outbound HTTP → no WireMock / `TUnit.Mocks.Http` needed.
- No time-driven behavior on the assertion path → no `FakeTimeProvider`.
  The job-listing ordering test does use a 25 ms `Task.Delay` between
  creates to give DuckDB's TIMESTAMP column a deterministic ordering; this
  is the only time-sensitive bit.

**xUnit → TUnit migrations:** None — see "State on arrival" above.

**Refactors for testability:** None required beyond the two production
bug fixes themselves.

**Mutation score:** Carried forward. `~/.dotnet/tools/dotnet-stryker` is
installed but there's still no `stryker-config.json` in the repo. The
production files this PR touches (`DuckDbStore.Provisioning.cs` +
`ProvisioningEndpoints.cs`) are the natural first scope for a Stryker
config commit when one lands.

**Verification:**

- `dotnet build qyl.slnx` — 0 errors, ~108 warnings (analyzer baseline).
- `ProvisioningEndpointsTests` standalone — 17/17 across three consecutive
  runs (~1.0–1.6 s each). Zero flakes.
- Full functional tier (`-trait Category=Functional`) — 25/25 across three
  consecutive runs (Observe 8 + Provisioning 17; HealthUi has the
  `Functional` Collection but not the trait so doesn't show under this
  filter, but full-suite below confirms it).
- Full collector test suite (`-noTrait Category=regen`) — 37/37 (HealthUi 3
  + Observe 8 + Provisioning 17 + non-functional 9). No regressions.

**Gaps remaining for next run:**

1. *TypeSpec drift, ConfiguratorApi.* `core/specs/api/routes.tsp:817+`
   declares a different contract than `ProvisioningEndpoints.cs` ships:
   `listProfiles` is paged (`CursorPage<GenerationProfileEntity>` with
   `limit?`/`cursor?`), but the impl returns
   `{items, total}` with no pagination; spec has `POST /profiles`,
   `GET /selections?workspaceId=`, no `POST /jobs/{id}/cancel`, no
   `GET /jobs?workspaceId=`. The implementation is a handwritten
   superset/sub-set; the generated `ConfiguratorApiController` is not
   wired into the route table. This needs a separate alignment pass —
   either bring the impl back to spec, or update the spec to the
   shipping shape. Either path is bigger than a functional-test
   routine should take on solo.

2. *Off-spec `JobStatus` enum values.* `GenerationProfileService.Enqueue-
   JobAsync` initializes new jobs with `Status = "pending"`, but the
   TypeSpec `JobStatus` enum is `queued|running|completed|failed|
   cancelled`. The tests pin down "pending" because that's what the
   service emits today; aligning to "queued" should land with the
   TypeSpec sweep above.

3. *Duplicate provisioning services.* `Provisioning/ProfileService.cs` and
   `Provisioning/GenerationJobService.cs` exist alongside
   `GenerationProfileService.cs` but are not referenced by any endpoint.
   They define an `InstrumentationProfile` / `ConfigSelectionRequest`
   record set that duplicates the `GenerationProfile` /
   `GenerationSelectionRequest` set used by the live endpoints. Looks
   like dead code from an earlier rename/split. Worth confirming + deleting
   in a focused cleanup commit.

4. *Stryker.NET config.* Still not in the repo. Once landed and scoped to
   `services/qyl.collector/Provisioning/` + `Storage/DuckDbStore.Provi-
   sioning.cs`, the routine can satisfy its mutation-testing step.

5. *`add_index` change_type* (carry-forward from PR #360) — schema
   promotion path still untested for index-only changes.

6. *`qyl.mcp` service* — still zero functional coverage; needs its own
   factory shape (stdio + HTTP).

7. *Middleware contracts* (carry-forward from PR #360 and the bootstrap
   run) — `UseQylCollectorMiddleware` redaction / exception-capture /
   trace-emission paths still untested through the pipeline.

**Hard-constraint compliance:**

- Never pushed to `main`, never merged, did not approve own PR.
- No existing test deleted, skipped, or weakened.
- No edits to `AGENTS.md`, `CLAUDE.md`, `.editorconfig`, `.globalconfig`,
  CI YAML, `Directory.Build.props`, `Version.props`, or `global.json`.
- No real external services hit. Pure in-process boot, in-memory DuckDB.
- No unit / integration / e2e work bundled in.

**Handoff:** none. The carry-forward gaps above are functional-tier work
for the next run plus one TypeSpec alignment pass that belongs to its own
dedicated routine cycle.

## qyl-e2e-tests 2026-05-20 04:31

**Outcome:** BLOCKED — Docker daemon still not running. Second consecutive day.
Run stopped without changes. No code touched.

**Blocker:** identical cause as the 2026-05-19 entry below. OrbStack's Docker
socket is still missing (`/Users/ancplua/.orbstack/run/docker.sock` →
`No such file or directory`); `pgrep -fl -i 'OrbStack|com.docker'` returns no
daemon processes. The OrbStack app is installed but not started. Bringing the
desktop daemon up is the user's call, not the scheduled task's.

**State on arrival:**
- Worktree clean on `claude/eager-mahavira-9f48b4`. `origin/main` HEAD is
  `82c7ad63 refactor(qyl.mcp): delete dead ServiceProviderRef plumbing`
  (three commits ahead of the 2026-05-19 `c2ab2116` baseline; none touch the
  E2E perimeter — they are mcp wiring + auto-merge.yml sync).
- E2E project + last shipped scenario unchanged from 2026-05-18:
  `OtlpHttpTraceIngestionRoundtripTests` in `tests/qyl.e2e.tests`.
- Carry-forward priority-1 production bug **still present**:
  `spans.kind` / `spans.status_code` declared `VARCHAR NOT NULL` at
  `services/qyl.collector/Storage/DuckDbSchema.g.sql:313,317`, but
  `internal/qyl.collector.storage.generators/DuckDbEmitter.cs:184,221`
  emits `reader.Col(N).AsByte` / `GetByte(0)` for those columns →
  `InvalidCastException` on every `GET /api/v1/traces` row read. Not an E2E
  fix; flagged again so it does not silently age out.
- `dotnet build` not attempted; a build cannot unblock a missing daemon.

**Carry-forward gaps (unchanged from 2026-05-19; only restated when the
priority shifts):**
1. Priority-1 production bug above — needs its own focused PR with migration
   testing. Once landed, extend `OtlpHttpTraceIngestionRoundtripTests` to also
   assert `GET /api/v1/traces/{traceId}` returns the row.
2. MCP → collector handshake scenario (read previously-OTLP-ingested spans
   over JSON-RPC; round-trip assertion).
3. Chat ingest → trace at sink with credentials redacted (requires adding an
   OTel collector sink container with a file exporter to `QylTopologyFixture`).

**Pattern flag:** two consecutive Docker-down no-ops. If this becomes three,
worth considering whether the routine should attempt a non-interactive
`open -gja OrbStack` (still user-side action — agent only nudges, never
escalates to `osascript` automation without explicit instruction). Not doing
it today; the prior entry's reasoning stands.

**Handoff:** none.

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

## qyl-unit-tests 2026-05-22 10:49

**Outcome:** first unit-test increment shipped. PR #363. One commit on
`tests/auto-unit-2026-05-22`.

**State on arrival:**
- `dotnet build qyl.slnx` — 0 errors. Clean baseline.
- Branch `claude/cranky-payne-9beacf` (worktree default) clean.
- No prior `tests/auto-unit-*` branch on the remote; this is the first
  run of `qyl-unit-tests`.
- Top of `main`: `a137bc2d Revert "chore(research): add thesis evaluation
  automation"`.

**Picked gap:** `services/qyl.mcp/Scoping/QylScopeInjector.cs` — last
touched 2026-05-19 in `41747d7b feat(qyl.mcp): consume ANcpLua.Agents.Mcp[.Hosting] 0.1.0 as fluent chain`,
security boundary for per-request `serviceName` / `sessionId` injection on
every MCP `tools/call`, zero prior tests. Highest-priority slot per
SKILL.md ("Recently changed production code with no corresponding unit
test").

**Changes shipped (PR #363 — 1 commit):**
- `f4d76352 test(unit/mcp/scoping): cover QylScopeInjector` — 17 cases
  across 11 `[Fact]` and one 6-case `[Theory]`. Pins the
  `IQylConstraintInjector<QylScope>` XML-doc contract: caller wins for
  non-empty strings, scope wins for `""` / numbers / bools / nulls /
  arrays / objects, mutate-in-place returns same reference, fallback dict
  is case-insensitive, per-field independence between `ServiceName` and
  `SessionId`. No refactor commit needed — class already pure-logic and
  `QylScope.ForTest` already exposed via `internal static` +
  `InternalsVisibleTo("Qyl.Mcp.Tests")`.

**SKILL.md vs CLAUDE.md conflict (resolved):**
- SKILL.md prescribes TUnit migration as the first step in any file
  touched.
- CLAUDE.md is explicit: "xUnit v3 with Microsoft Testing Platform". All
  five test projects in the repo (`qyl.collector.tests`,
  `qyl.collector.integration.tests`, `qyl.mcp.tests`, `qyl.e2e.tests`,
  `qyl.opentelemetry.semconv.sourcegen.tests`) reference
  `xunit.v3.mtp-v2`, never `TUnit`.
- SKILL.md §"Read before you touch anything" item 1 defers to CLAUDE.md:
  "They override anything in this file where they conflict." Followed
  CLAUDE.md, wrote xUnit v3.

**Mutation testing — BLOCKED upstream:**
- Bootstrapped `dotnet-stryker` 4.14.2 via local tool manifest
  (`.config/dotnet-tools.json`) + `stryker-config.json` per SKILL.md
  template.
- First run failed:
  `Project '…/qyl.mcp.tests.csproj' is using Microsoft.Testing.Platform
  which is not yet supported by Stryker, see
  https://github.com/stryker-mutator/stryker-net/issues/3094`
- 4.14.2 is the latest stable. `dotnet-stryker-netx` (3.x port) is older.
  No CLI override exists to bypass VsTest.
- Per SKILL.md exit clause "Stryker is failing to run and you can't
  unblock it in a few minutes → revert Stryker-config changes, note,
  exit" — bootstrap was reverted; PR contains zero Stryker artifacts.
- Compensating control: walked each Stryker-typical mutation against the
  production source by hand. Surfaced one survivor (the fallback dict's
  `StringComparer.OrdinalIgnoreCase` comparer) and added
  `Inject_NewlyCreatedDict_IsCaseInsensitive` to kill it. One arguably-
  equivalent mutant remains on the static `s_injectableParameters`
  comparer (lowercase-only keys make the choice behaviorally invisible).

**Verification:**
- `dotnet build qyl.slnx` — 0 errors after the test was added.
- `dotnet test --project tests/qyl.mcp.tests --no-build` — 19/19 passing
  (17 new in `QylScopeInjectorTests`, 2 existing in
  `SummaryCredentialRedactorTests`). 0.6s wall clock.
- Filtered run on the new class alone — 17/17 passing.

**Gaps remaining for next run:**
1. **Stryker / MTP incompatibility is now a routine-wide blocker.** Until
   either (a) `stryker-mutator/stryker-net#3094` lands MTP support,
   (b) qyl adds a parallel non-MTP test surface for Stryker only, or
   (c) the routine switches to a different mutation tool, **no
   `qyl-*-tests` routine can do mutation testing.** Track upstream issue
   and revisit. This is the same wall the functional-tests routine hit
   on 2026-05-18.
2. **`InvestigationLineage`, `InvestigationGuard`** — three- and
   four-line wrappers in `services/qyl.mcp/Agents/` around
   `EnvConfig.ReadInt` and the governance types from
   `ANcpLua.Agents.Governance`. Cheap parameterized tests pinning the
   env-var → governance-config mapping (depth 3 default, spawn 10
   default, min 1 floor).
3. **`QylScope.FromEnvironment`** — the only non-test scope factory.
   Trim-and-empty-handling for `QYL_SERVICE` / `QYL_SESSION`. Touches
   `Environment.GetEnvironmentVariable`, so it would need either an env
   override pattern or accept the implicit-environment side effect.
4. **`Formatting/ResponseFormatter`, `Formatting/ErrorFormatter`** —
   `qyl.mcp` LLM-coaching formatters (the LLM-result-cap pattern
   documented in `services/qyl.mcp/CLAUDE.md`). Pure text-shaping, no
   existing tests.
5. **`Capabilities/QylCapabilityCatalog`, `Skills/QylSkillCatalog`** —
   manifest enumeration generators. Worth a smoke test that the manifest
   contains the documented count plus at least the documented names.
6. **`Scoping/ScopingDelegatingHandler`** — sibling of `QylScopeInjector`
   on the outbound HTTP side. Adjacent target now that the inbound
   injector is covered.

**Handoff:** none. PR #363 is open and awaits review/merge; do not push
to `main` from this routine.
