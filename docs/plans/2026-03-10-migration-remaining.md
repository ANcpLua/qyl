# Migration Remaining Work

**Branch:** `feature/loom-decomposition` on `qyl`
**Source:** `qyl.loom` (`/Users/ancplua/RiderProjects/qyl.loom/`)
**Target:** `qyl` (`/Users/ancplua/qyl/`)
**Goal:** Merge qyl.loom architectural improvements into qyl, then archive qyl.loom.

## Current State

- Build: **0 errors, 0 warnings** (12 projects)
- Tests: 7 pass, 13 fail (all 13 failures are pre-existing in qyl.loom — not migration regressions)
- Removed: `qyl.copilot` (decomposed into `qyl.agents` + `qyl.workflows`)
- Removed: `qyl.loom` empty shell project (types live in `qyl.collector`)
- Created: `qyl.collector.storage.generators` (DuckDB codegen, isolated from OTel generators)

## What's Left

### Must-fix before merge

| # | Item | Files |
|---|------|-------|
| 1 | **NetArchTest.Rules not in CPM** — user added to test csproj, needs Version.props + Directory.Packages.props entry | `Version.props`, `Directory.Packages.props`, `tests/qyl.collector.tests/qyl.collector.tests.csproj` |
| 2 | **Stale Dockerfile paths** — references `qyl.protocol`, `qyl.servicedefaults`, `qyl.copilot` | `src/qyl.collector/Dockerfile` |
| 3 | **CI workflow** — verify `ci.yml` builds/tests correctly with new project structure | `.github/workflows/ci.yml` |
| 4 | **loom-design.md stale names** — still references `qyl.servicedefaults` (lines 1398-1399) | `docs/roadmap/loom-design.md` |

### Pre-existing bugs (from qyl.loom, not caused by migration)

| # | Item | Impact |
|---|------|--------|
| 5 | **DuckDB logs schema mismatch** — `DuckDbStore.InsertLogsAsync` expects `log_id`, `session_id`, `service_name` columns not in DDL | 7 test failures |
| 6 | **OTel double UseOtlpExporter** — test `WebApplicationFactory` registers OTLP exporter twice | 6 test failures (HealthEndpointTests) |
| 7 | **SpanAppender deleted** — replaced with multi-row INSERT (perf regression on hot path) | Throughput |
| 8 | **Generated DDL uses DOUBLE for byte fields** — `kind`, `status_code` should be UTINYINT | 8x storage waste |
| 9 | **Dual IChatClient factories** — `AgentLlmFactory` (mcp) vs `LlmProviderFactory` (agents) | Code duplication |
| 10 | **9 overlapping JSON serializable types** across `CopilotJsonContext` and `QylSerializerContext` | Metadata drift risk |

### Cleanup (low priority)

| # | Item |
|---|------|
| 11 | `qyl.watch` still uses lowercase `namespace qyl.watch` (8 files) |
| 12 | Generated attribute files use lowercase `namespace qyl.contracts.Attributes` |
| 13 | `DomainContracts.g.cs` uses mixed-case `namespace qyl.Contracts` |
| 14 | `src/qyl.copilot/qyl.loom/` nested leftover in qyl.loom source repo |
