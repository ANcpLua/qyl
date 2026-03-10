# Loom Decomposition: Merge qyl.loom into qyl

**Date:** 2026-03-10
**Status:** Proposed
**Author:** ANcpLua + Claude

## 1. Summary

Apply qyl.loom's architectural improvements as a branch in qyl. qyl already has the full infrastructure (CI/CD, analyzer configs, GitHub repo, Railway deployment). qyl.loom has 15 code-level improvements. Merge direction: qyl.loom → qyl.

## 2. Zero Public Users — Evidence

| Check | Result |
|-------|--------|
| GitHub visibility | Private (`gh api repos/ancplua/qyl → .private = true`) |
| Stars / Forks | 0 / 0 |
| Published NuGet | None |
| Docker Hub | `ancplua/qyl:latest` — personal image, no third-party consumers |
| Railway | Self-hosted personal project |

One consumer. No coordination needed.

## 3. What qyl.loom Brings (the delta)

### 3.1 Project Renames

| qyl (current) | After merge | Rationale |
|---------------|-------------|-----------|
| `qyl.protocol` | `qyl.contracts` | .NET convention; "protocol" conflicts with OTLP protocol |
| `qyl.servicedefaults` | `qyl.instrumentation` | "servicedefaults" is an Aspire template artifact |
| `qyl.instrumentation.generators` | `qyl.instrumentation.generators` | `{library}.generators` Roslyn convention |

### 3.2 New Extracted Projects

| Project | Type | Dependencies | Extracted From |
|---------|------|-------------|----------------|
| `qyl.agents` | Library | contracts, instrumentation, instrumentation.generators (analyzer) | qyl.copilot (minus Workflows) |
| `qyl.workflows` | Library | contracts, agents | qyl.copilot/Workflows/ |
| `qyl.loom` | Library | collector, agents, workflows, contracts, instrumentation | qyl.collector (Loom features) |

### 3.3 New Source Generators

| File | Purpose |
|------|---------|
| `qyl.instrumentation.generators/DuckDb/DuckDbAttributes.cs` | Marker attributes for DuckDb tables |
| `qyl.instrumentation.generators/DuckDb/DuckDbEmitter.cs` | Roslyn emitter for type-safe inserts |
| `qyl.instrumentation.generators/DuckDb/DuckDbInsertGenerator.cs` | IIncrementalGenerator — compile-time DuckDb |
| `qyl.instrumentation.generators/Generated/DomainContracts.g.cs` | Generated domain contract types |

### 3.4 Other Changes

| Change | Detail |
|--------|--------|
| `Agents/AgentLlmFactory.cs` added to qyl.mcp | Replaces `Providers/LlmProviderFactory.cs` |
| `Tools/TelemetryJsonContext.cs` added to qyl.mcp | AOT-compatible `JsonSerializerContext` |
| `Storage/DuckDbSchema.WorkflowRuns.cs` added to collector | Workflow run persistence |
| `eng/scripts/qyl-verify.sh` added | Verification script |
| `Storage/SpanAppender.cs` removed from collector | Replaced by generated DuckDb inserts |
| `McpHostOptions.cs` removed from qyl.mcp | Absorbed into builder |
| `Agents/SessionSummaryPrompt.cs` removed from qyl.mcp | Consolidated |

## 4. Dependency Graph After Merge

```
qyl.contracts (leaf, BCL-only, zero dependencies)
     ↑
qyl.instrumentation → contracts, instrumentation.generators (analyzer)
     ↑
qyl.agents → contracts, instrumentation, instrumentation.generators (analyzer)
     ↑
qyl.workflows → contracts, agents
     ↑
qyl.collector → agents, workflows, contracts, instrumentation, instrumentation.generators (analyzer)
     ↑
qyl.loom → collector, agents, workflows, contracts, instrumentation

qyl.mcp → contracts (HTTP to collector — boundary preserved)
```

## 5. Antipatterns Fixed

| Before | After |
|--------|-------|
| `qyl.copilot` mixes agents + workflows + streaming | `qyl.agents` (AI) + `qyl.workflows` (engine) |
| Collector owns 40+ feature directories | Loom features in `qyl.loom` project |
| Runtime DuckDb appends (`SpanAppender.cs`) | Compile-time generated inserts (`DuckDbInsertGenerator`) |
| `qyl.servicedefaults` name leaks Aspire detail | `qyl.instrumentation` |
| `qyl.protocol` ambiguous with OTLP | `qyl.contracts` |
| No AOT serialization in MCP | `TelemetryJsonContext.cs` |

## 6. What Already Matches (no porting needed)

Both repos share identical infrastructure — confirmed by direct file comparison:

- `.globalconfig` (100+ analyzer rules at error severity)
- `.editorconfig` (17k formatting/severity config)
- `.aiexclude`, `.claudeignore`
- `.codecov.yml`, `.markdownlint.json`
- `.github/workflows/` (6 CI/CD workflows)
- `.nuke/parameters.json`
- `.mcp.json`
- `eng/` build system, `Version.props`, `Directory.Packages.props`

**Only missing from qyl.loom** (minor, port during merge):
- `.qyl/workflows/` (declarative AG-UI workflows)
- `.codex/` (Codex agent config)
- `.codex-screenshots/`, `.codex-screenshots-advanced/` (16 design variants)

## 7. Merge Sequence

### Step 1: Branch

```bash
cd /Users/ancplua/qyl
git checkout main
git checkout -b feature/loom-decomposition
```

### Step 2: Rename Projects

1. Rename `src/qyl.protocol/` → `src/qyl.contracts/`
   - Update csproj filename, `RootNamespace`, `PackageId`
   - Update all `<ProjectReference>` paths across solution
2. Rename `src/qyl.servicedefaults/` → `src/qyl.instrumentation/`
   - Update csproj, `RootNamespace`, `InterceptorsNamespaces`
3. Rename `src/qyl.instrumentation.generators/` → `src/qyl.instrumentation.generators/`
   - Update csproj, namespace

### Step 3: Add Extracted Projects

1. Copy `src/qyl.agents/` from qyl.loom
2. Copy `src/qyl.workflows/` from qyl.loom
3. Copy `src/qyl.loom/` from qyl.loom (the project, not the repo)
4. Add all three to `qyl.slnx`

### Step 4: Add DuckDb Generators

1. Copy `qyl.instrumentation.generators/DuckDb/` from qyl.loom
2. Copy `qyl.instrumentation.generators/Generated/DomainContracts.g.cs` from qyl.loom

### Step 5: Apply MCP Changes

1. Add `Agents/AgentLlmFactory.cs` (from qyl.loom)
2. Add `Tools/TelemetryJsonContext.cs` (from qyl.loom)
3. Delete `Providers/LlmProviderFactory.cs`
4. Delete `McpHostOptions.cs`
5. Delete `Agents/SessionSummaryPrompt.cs`

### Step 6: Apply Collector Changes

1. Add `Storage/DuckDbSchema.WorkflowRuns.cs` (from qyl.loom)
2. Delete `Storage/SpanAppender.cs`

### Step 7: Clean Up Dead Code

1. Delete `src/qyl.copilot/` entirely
2. For each duplicated directory (AgentRuns, Analytics, Autofix, CodingAgent, Errors, Identity, Workflow):
   - Diff collector copy vs qyl.loom copy
   - Keep authoritative version in `src/qyl.loom/`
   - Delete from collector
3. Remove `qyl.copilot` from `qyl.slnx` if still referenced

### Step 8: Update Solution

1. Update `qyl.slnx`:
   - Remove: `qyl.copilot`
   - Add: `qyl.agents`, `qyl.workflows`, `qyl.loom`
   - Rename paths for contracts, instrumentation, instrumentation.generators
2. Add `tests/qyl.collector.tests/` if missing

### Step 9: Verify

```bash
eng/build.sh Compile    # 0 errors, 0 warnings
eng/build.sh Test       # all pass
docker build -f src/qyl.collector/Dockerfile .  # image builds
```

Grep for stale references:
```bash
grep -r "qyl\.protocol" --include="*.cs" --include="*.csproj" src/
grep -r "qyl\.servicedefaults" --include="*.cs" --include="*.csproj" src/
grep -r "qyl\.copilot" --include="*.cs" --include="*.csproj" --include="*.slnx" .
grep -r "Qyl\.ServiceDefaults\.Generator" --include="*.cs" src/
```

### Step 10: PR

```bash
git add -A
git commit -m "feat: decompose collector into qyl.agents + qyl.workflows + qyl.loom"
git push -u origin feature/loom-decomposition
gh pr create --title "Loom decomposition" --body "See docs/plans/2026-03-10-qyl-to-qyl-loom-migration.md"
```

## 8. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Namespace breaks after renames | Medium | Build fails | Step 9 catches it |
| Duplicated files diverged | Medium | Wrong behavior | Diff in Step 7 |
| Dockerfile paths change | Low | Docker fails | Collector path unchanged |
| CI workflow references old names | Low | CI fails | Grep in Step 9 |

## 9. Verification Checklist

- [ ] `src/qyl.copilot/` deleted
- [ ] `src/qyl.protocol/` renamed to `src/qyl.contracts/`
- [ ] `src/qyl.servicedefaults/` renamed to `src/qyl.instrumentation/`
- [ ] `src/qyl.instrumentation.generators/` renamed to `src/qyl.instrumentation.generators/`
- [ ] `src/qyl.agents/`, `src/qyl.workflows/`, `src/qyl.loom/` added
- [ ] No grep hits for `qyl.protocol`, `qyl.servicedefaults`, `qyl.copilot`, `Qyl.Instrumentation.Generators`
- [ ] `qyl.slnx` updated with all new projects
- [ ] Collector has zero duplicated Loom directories
- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] All tests pass
- [ ] Docker image builds
- [ ] OTLP smoke test passes

## 10. After Merge

1. ~~Delete empty worktrees (`wt-backend`, `wt-frontend`, `wt-loom`, `wt-mcp`)~~ **DONE** (2026-03-10)
2. Delete or archive the local qyl.loom repo
3. Move this doc to `docs/done/`
