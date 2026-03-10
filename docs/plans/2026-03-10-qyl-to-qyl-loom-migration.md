# Loom Decomposition: Finish the In-Repo Migration

**Date:** 2026-03-10  
**Status:** Proposed (Rebased to Current Tree)  
**Author:** ANcpLua + Claude + Codex

## 1. Summary

This is no longer a greenfield `qyl.loom -> qyl` merge plan.

The current `qyl` repo already contains the decomposed project set:

- `qyl.contracts`
- `qyl.instrumentation`
- `qyl.instrumentation.generators`
- `qyl.collector.storage.generators`
- `qyl.agents`
- `qyl.workflows`
- `qyl.loom`

The solution file is already on the new names. The remaining work is to:

1. finish consumer migration,
2. remove stale legacy references and build assets,
3. preserve strict architecture boundaries with automated tests,
4. avoid deleting still-live compatibility code prematurely.

The migration should therefore be treated as **completion and cleanup**, not as a first-pass rename/extract.

Feature readiness, MCP audit status, and Loom-specific open issues are tracked separately in
`docs/roadmap/loom-status.md`. This document only covers migration and boundary work.

## 2. Current State Snapshot

### Already true in the current tree

- `qyl.slnx` already includes `qyl.contracts`, `qyl.instrumentation`, `qyl.instrumentation.generators`,
  `qyl.collector.storage.generators`, `qyl.agents`, `qyl.workflows`, and `qyl.loom`.
- `qyl.collector` already references the new projects.
- `qyl.collector.storage.generators` already exists and contains the DuckDB generator files:
    - `DuckDbAttributes.cs`
    - `DuckDbEmitter.cs`
    - `DuckDbInsertGenerator.cs`
- `qyl.instrumentation.generators` already appears clean of DuckDB code and contains `Generated/DomainContracts.g.cs`.
- `qyl.mcp` already references `qyl.contracts` and already has active remote MCP HTTP support.

### Still needing completion

- Dockerfiles and some build assets still reference deleted legacy paths such as `qyl.protocol`, `qyl.servicedefaults`,
  and `qyl.copilot`.
- Compatibility/runtime code still exists that must not be deleted blindly:
    - `src/qyl.mcp/McpHostOptions.cs`
    - `src/qyl.mcp/Agents/SessionSummaryPrompt.cs`
- Some public-facing or semi-public strings still use `qyl.copilot` naming.
- Architectural boundaries are not yet enforced by automated tests.
- Use `docs/roadmap/loom-status.md` for current capability and MCP readiness instead of adding those tables here.

## 3. Hard Architectural Rules

These are the non-negotiables for the rest of the migration.

### 3.1 Contracts stay leaf-only

`qyl.contracts` must remain BCL-only and dependency-free.

### 3.2 Instrumentation and storage generation stay separate

`qyl.instrumentation.generators` must remain generic telemetry/instrumentation only.  
`qyl.collector.storage.generators` must own DuckDB-specific source generation.

No DuckDB code, symbols, or references are allowed in `qyl.instrumentation.generators`.

### 3.3 MCP stays runtime-HTTP to collector only

`qyl.mcp` may call collector over HTTP at runtime.  
`qyl.mcp` must not gain a `ProjectReference` to `qyl.collector`.

### 3.4 Do not break active compatibility code without replacement

Do not delete:

- `McpHostOptions.cs` until its behavior is fully absorbed elsewhere
- `SessionSummaryPrompt.cs` until its call sites are migrated
- legacy tool names like `qyl.copilot_*` unless the contract rename is explicit and versioned

## 4. What This Plan Is Not

This plan is not:

- a fresh rename of `qyl.protocol -> qyl.contracts`
- a fresh rename of `qyl.servicedefaults -> qyl.instrumentation`
- a fresh extraction of `qyl.agents`, `qyl.workflows`, `qyl.loom`
- a same-PR cleanup of local worktrees or repo archival

Those first three steps are already materially present in the tree. The remaining work is integration cleanup and
enforcement.

## 5. Recommended Execution Strategy

Use one branch and multiple small commits, not one destructive sweep.

**Branch name:** `dev/loom-decomposition`

### Phase 0: Freeze and Inventory

1. Create the working branch from `main`.
2. Capture a baseline compile and test result using the repo build entrypoints.
3. Inventory all remaining stale references and classify them into:
    - build/config references
    - runtime code references
    - public contract strings
    - dead code

**Important:** A plain text grep hit is not automatically a bug.  
For example, `qyl.copilot` inside an MCP tool name may be an intentional contract, while `qyl.copilot` inside a Docker
`COPY` path is stale infrastructure.

### Phase 1: Confirm Current Authorities

Treat the following as current authorities unless a diff proves otherwise:

- `qyl.contracts` over any old `protocol` naming
- `qyl.instrumentation` over any old `servicedefaults` naming
- `qyl.instrumentation.generators` for generic telemetry generation
- `qyl.collector.storage.generators` for DuckDB generation
- `qyl.agents` for agent infrastructure
- `qyl.workflows` for workflow engine code
- `qyl.loom` for Loom-specific slice code

Do not re-extract or re-rename these projects again.

### Phase 2: Finish Consumer Migration

Focus on the remaining places that still point at legacy names or legacy ownership.

#### 2.1 Build and packaging assets

Update all Dockerfiles, scripts, and build assets that still copy or reference:

- `src/qyl.protocol`
- `src/qyl.servicedefaults`
- `src/qyl.copilot`

This is now the highest-confidence cleanup because those legacy source directories no longer exist.

#### 2.2 MCP-specific cleanup

Apply MCP changes only if they preserve current behavior.

- Keep `McpHostOptions.cs` until the remote HTTP path is fully preserved by another mechanism.
- Keep `SessionSummaryPrompt.cs` until `ReplayTools` no longer uses it.
- If adding `TelemetryJsonContext.cs`, do it only where it replaces a real serialization gap.
- If `AgentLlmFactory.cs` is already present, treat it as current state, not as new work.

#### 2.3 Collector and Loom boundary cleanup

Move remaining Loom-specific authority into `qyl.loom` only when the collector call sites have been redirected and
verified.

Do not delete collector-side logic merely because a similarly named Loom file exists.  
Require one of:

- direct consumer migration to `qyl.loom`, or
- a proof diff showing the collector copy is dead and redundant.

### Phase 3: Safe Deletion of Residual Legacy Code

Only after Phases 1 and 2 are green:

1. Delete dead compatibility code that has no active call sites.
2. Remove stale project references and `using` directives.
3. Remove dead build assets that reference deleted paths.

This phase is where ruthless deletion belongs.  
It does **not** belong before migration proof.

### Phase 4: Add Automated Architecture Tests

Add architecture tests to prevent regression.

Suggested assertions:

```csharp
[Fact]
public void Agents_Should_Not_Depend_On_Loom_Or_Collector()
{
    var result = Types.InAssembly(typeof(Qyl.Agents.Agents.QylAgentBuilder).Assembly)
        .ShouldNot()
        .HaveDependencyOnAny("Qyl.Loom", "Qyl.Collector")
        .GetResult();

    Assert.True(result.IsSuccessful);
}

[Fact]
public void Workflows_Should_Not_Depend_On_Loom_Or_Collector()
{
    var result = Types.InAssembly(typeof(Qyl.Workflows.WorkflowServiceExtensions).Assembly)
        .ShouldNot()
        .HaveDependencyOnAny("Qyl.Loom", "Qyl.Collector")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

Also add guards for the source generators:

- `qyl.instrumentation.generators` must not depend on `Qyl.Collector` or `Qyl.Collector.Storage.Generators`
- `qyl.collector.storage.generators` must not depend on `Qyl.Instrumentation` or `Qyl.Instrumentation.Generators`
- `qyl.mcp` must not reference `qyl.collector.csproj`

In addition to assembly dependency tests, add a low-tech content check in CI:

```bash
rg -n "DuckDb|DuckDB" src/qyl.instrumentation.generators && exit 1
```

### Phase 5: Verify

Use the repo build entrypoints, not ad-hoc `dotnet test`.

```bash
eng/build.sh Compile
eng/build.sh Test
docker build -f src/qyl.collector/Dockerfile .
docker build -f src/qyl.mcp/Dockerfile .
```

Then run targeted stale-reference checks:

```bash
rg -n "qyl\.protocol|qyl\.servicedefaults|qyl\.copilot" src/ eng/ .github/
rg -n "DuckDb|DuckDB" src/qyl.instrumentation.generators
rg -n "qyl\.collector" src/qyl.mcp/*.csproj src/qyl.mcp/**/*.cs
```

Interpretation rule:

- hits in Dockerfiles or build scripts usually mean stale infrastructure
- hits in tool names or UI labels may be intentional contracts and require a rename decision, not blind deletion

## 6. Explicit Do-Not-Do List

Do not:

- recreate already-existing projects as if they were missing
- add DuckDB generator code back into `qyl.instrumentation.generators`
- give `qyl.mcp` a project reference to `qyl.collector`
- delete `McpHostOptions.cs` or `SessionSummaryPrompt.cs` before replacing their behavior
- rename public MCP tool names in the same refactor unless the contract migration is intentional
- delete local worktrees or archive the local `qyl.loom` repo in the same PR as the code migration

## 7. Concrete Remaining Tasks

This is the practical shortlist for the next implementation pass.

- Fix stale Dockerfile copy paths in:
    - `src/qyl.collector/Dockerfile`
    - `src/qyl.mcp/Dockerfile`
- Audit remaining `qyl.copilot` strings and classify them:
    - public contract to keep
    - stale naming to rename
- Audit `qyl.mcp` for deletions proposed by the older doc and remove only what is truly dead
- Add architecture tests to `tests/qyl.collector.tests`
- Run compile, tests, Docker builds, and stale-reference checks

## 8. Risk Assessment

| Risk                                                   | Likelihood | Impact                   | Mitigation                                         |
|--------------------------------------------------------|------------|--------------------------|----------------------------------------------------|
| Deleting still-live compatibility code                 | High       | Build/runtime break      | Require call-site proof before deletion            |
| Reintroducing DuckDB into instrumentation generators   | Medium     | Architectural regression | Assembly tests + grep guard                        |
| MCP/collector boundary erosion                         | Medium     | Layer violation          | Explicit no-project-reference test                 |
| Renaming public `qyl.copilot_*` contracts accidentally | Medium     | External behavior break  | Treat names as contract until explicitly versioned |
| Stale Dockerfiles breaking container builds            | High       | CI/deploy failure        | Fix before deletion PR closes                      |

## 9. Verification Checklist

- [ ] `qyl.contracts`, `qyl.instrumentation`, `qyl.instrumentation.generators`, `qyl.collector.storage.generators`,
  `qyl.agents`, `qyl.workflows`, and `qyl.loom` remain the authoritative project set
- [ ] `qyl.instrumentation.generators` contains zero DuckDB code
- [ ] `qyl.collector.storage.generators` remains DuckDB-only
- [ ] `qyl.mcp` keeps HTTP-runtime-only dependency on collector
- [ ] Dockerfiles no longer reference deleted legacy paths
- [ ] no dead compatibility files remain without a call site
- [ ] architecture tests enforce boundaries
- [ ] compile passes
- [ ] tests pass
- [ ] collector and MCP Docker images build

## 10. Post-Merge Cleanup

After the code migration is green:

1. delete or archive the separate local `qyl.loom` repo if it is no longer needed
2. clean up stale worktrees only if they are truly obsolete
3. move this document to `docs/done/`

Do not combine those housekeeping steps with the main refactor PR.
