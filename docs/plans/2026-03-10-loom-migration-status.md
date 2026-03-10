# Loom Migration Status

**Date:** 2026-03-10
**Branch:** `feature/loom-decomposition`
**State:** 371 uncommitted files, not committed

## Done

### Stale Reference Cleanup
- `src/qyl.mcp/Dockerfile`: `qyl.protocol` → `qyl.contracts`
- `src/qyl.collector/Dockerfile`: added `qyl.collector.storage.generators` COPY lines
- `.github/workflows/ci.yml`: `qyl.protocol/` → `qyl.contracts/`, `qyl.servicedefaults/` → `qyl.instrumentation/`
- `eng/semconv/generate-semconv.ts`: output paths updated to `qyl.instrumentation/`
- `eng/semconv/qyl-extensions.json`: output paths + namespaces updated to `qyl.contracts/`
- `.github/copilot-instructions.md`: project table updated with new names
- `CLAUDE.md`: architecture diagram, dependency chain, dependency rules all updated
- `.claude/rules/mcp-server.md`: `qyl.protocol` → `qyl.contracts`
- 4 source comments in `qyl.instrumentation/`: `qyl.servicedefaults` → `qyl.instrumentation`

### Architecture Tests
- Added `NetArchTest.Rules` 1.3.2 via CPM (`Version.props` + `Directory.Packages.props`)
- Created `tests/qyl.collector.tests/ArchitectureTests.cs` with 4 tests:
  1. Agents must not depend on Loom or Collector
  2. Workflows must not depend on Loom or Collector
  3. Contracts must not depend on any project
  4. MCP must not reference Collector assembly
- All 4 pass

### qyl.loom Source Copy (then reverted)
- Copied 7 directories (49 files) from `/Users/ancplua/RiderProjects/qyl.loom/src/qyl.loom/`
- Discovered all files use `namespace Qyl.Collector.*` — they are collector duplicates, not Loom-specific code
- Deleted all 7 directories. qyl.loom stays as empty shell (csproj + GlobalUsings.cs)

### Boundary Verification
- `qyl.instrumentation.generators`: 0 DuckDB hits
- `qyl.mcp/*.csproj`: 0 `qyl.collector` ProjectReference hits
- `qyl.copilot_*` MCP tool names: kept as intentional public contracts

## Not Done

1. **Commit the 371 uncommitted changes** on `feature/loom-decomposition`
2. **Docker image builds** — not tested (needs Docker daemon running)
3. **13 pre-existing test failures** — `HealthEndpointTests` (5) and `LogSummaryServiceTests` (8) fail because they need DuckDB at runtime. Not caused by migration.
4. **Migrate Loom-specific code INTO qyl.loom with `Qyl.Loom.*` namespaces** — the 7 feature domains (AgentRuns, Analytics, Autofix, CodingAgent, Errors, Identity, Workflow) still live in `qyl.collector`. Moving them to `qyl.loom` requires redirecting all collector call sites first (Phase 2.3 of the plan).
5. **CodingAgent bug in qyl.loom repo** — the qyl.loom repo's `CodingAgentEndpoints.cs` uses `Enum.TryParse` instead of `CodingAgentProviderNames.TryParse`, which produces wrong database values. If Loom code is ever migrated, this must be fixed.
6. **Move migration plan to `docs/done/`** after merge
