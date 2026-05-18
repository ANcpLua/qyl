# Routine Test Run — 2026-05-18

## qyl-unit-tests 2026-05-18

### Status: SUCCESS (one increment shipped, draft-free PR opened, not merged)

### What ran

Build was green on arrival (`dotnet build qyl.slnx`: 0 errors, 1393
warnings — typical analyzer noise; pre-existing). Picked one concrete
gap from `services/qyl.mcp` (139 source files, 1 pre-existing test file
— sparsest test surface in the repo).

Target: `ConstraintInjector.InjectScope`
(`services/qyl.mcp/Scoping/ConstraintInjector.cs:14`). Pure-static, 25
lines, 5 distinct branches, untested. Caller: the JSON-RPC
scope-injection filter chained after `UseQylMcpInstrumentation` in
`QylMcpServerRegistration.cs:83`. `InternalsVisibleTo("Qyl.Mcp.Tests")`
already wired; `QylScope.ForTest(...)` already exists. No refactor for
testability needed.

Added `tests/qyl.mcp.tests/Scoping/ConstraintInjectorTests.cs` — 12
xUnit v3 tests (9 facts + 1 theory × 3 inline data), 67ms. Manual
mutation analysis in PR body covers every meaningful mutant against the
SUT's 25 lines.

### Convention conflict resolved

Scheduled-task description says "use TUnit". Repo `CLAUDE.md` says
"xUnit v3 with Microsoft Testing Platform" — and the one pre-existing
test file in `tests/qyl.mcp.tests/` is xUnit v3. Per the
`using-superpowers` priority rule (CLAUDE.md > skills > defaults),
stayed on xUnit v3 for this run. Flagged in PR body — if TUnit
migration is actually intended, that's a deliberate decision worth
making explicitly rather than per-file as a side effect of any unit-test
routine run.

### Stryker.NET — deferred

Not bootstrapped. Adding `.config/dotnet-tools.json` +
`stryker-config.json` is a repo-wide tooling change distinct from a
single coverage increment, and the routine's "Stryker can't unblock
quickly → exit" escape hatch applied. PR body contains a manual mutant
walk-through that covers every meaningful mutation against the SUT.
Bootstrap is the highest-value next-run task — once installed, the same
mutant analysis could be machine-verified per file.

### Outputs

- Branch: `tests/auto-unit-2026-05-18` (pushed)
- PR: https://github.com/O-ANcppLua/qyl/pull/350 (not merged)
- Worktree: `.claude/worktrees/reverent-matsumoto-aa03e6/` (left clean,
  no agent-created staged changes or temp files)

### Targets for the next run (ranked)

1. **Bootstrap Stryker.NET** — own session/PR. Touches root tooling
   files and likely needs a Nuke `Mutation` target wrapping it. Without
   this, every future unit-test PR has to ship the manual mutant table
   that #350 carries.
2. **`McpAdminToolFilter`** (40 lines, `services/qyl.mcp/Auth/`) —
   sibling of `ConstraintInjector` on the same filter chain. Should be
   the same shape of test: pure logic, pre-existing internal
   visibility, fast.
3. **`InvestigationLineage` / `InvestigationGuard`** (12 + 10 lines,
   `services/qyl.mcp/Agents/`) — currently env-coupled through
   `EnvConfig.ReadInt`. Refactor-for-testability move (wrap the env
   reader) goes in a separate commit before adding tests.
4. **Prompt classes** in `services/qyl.mcp/Agents/` (`RcaPrompt`,
   `ErrorSummaryPrompt`, `TraceSummaryPrompt`, `SessionSummaryPrompt`)
   — if any contain non-trivial templating, they're pure string-output
   targets. Quick scan first to confirm there's logic to test, not just
   a constant.
5. **`services/qyl.loom`** and **`services/qyl.loom.patterns`** have NO
   test project. First test would mean a brand-new csproj — out of
   scope for a single-increment routine run, but worth a dedicated
   session.

### Handoff notes

None. No findings that belong to a different routine
(`qyl-functional-tests`, `qyl-integration-tests`, `qyl-e2e-tests`)
surfaced during the scan. `.agents/routine-handoff.md` not created.
