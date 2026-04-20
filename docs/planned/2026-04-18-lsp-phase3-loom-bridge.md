# PLANNED — LSP Phase 3: Loom bridge

**Status:** Planned. Do **not** start before Phase 1 has been in production ≥ 1 week with real usage. Phase 2 (
TypeScript) is independent — can land before or after.
**Predecessor:** `.claude/handoffs/2026-04-17-spec3-lsp-phase1.md` (Phase 1 completed).
**Unlocks:** Loom workflows use deterministic code intelligence (goto / find refs / diagnostics / rename) instead of
grep + heuristic search.

## Outcome

Loom executors can call `LoomLspGotoDefinition`, `LoomLspFindReferences`, `LoomLspSymbols`, `LoomLspDiagnostics`,
`LoomLspPrepareRename`, `LoomLspRename` as first-class `[LoomTool]` functions with proper side-effect classification,
capability gating, and approval gating on the write path.

## Scope

Additive only — the MCP LSP surface from Phase 1 stays unchanged. This phase creates **wrappers**, not new
implementations.

- **New file:** `src/qyl.loom/Tools/LoomLspTools.cs` (sibling of `src/qyl.loom/Tools/LoomDetectors.cs`) with 6 static
  methods, each `[LoomTool]` + `[ToolSideEffect]` + `[RequiresCapability]` + `[EmitsStructuredOutput]` stacked per the
  existing `src/qyl.loom/CompilerDemo/LoomDemoWorkflow.cs` attribute pattern.
- **New contract types** co-located with the tool methods — these are Loom DTOs, not MCP DTOs. Can reuse the
  markdown-return path from Phase 1 if typed is too heavy.
- DI: Loom already reaches qyl.mcp's services via `IServiceProvider`. Resolution of `LspClientPool` in the Loom process
  requires either (a) same-process resolution if Loom hosts qyl.mcp's DI, or (b) an in-process facade. The Phase 1
  implementation made LspClientPool singleton — both paths are feasible.

**Do NOT touch:** the MCP tool class `LspTools` (Phase 1), `src/qyl.mcp/Tools/Lsp/**` (Phase 1),
`src/qyl.mcp/Hosting/**`.

## Attribute shape per tool

| Loom tool               | Phase     | Side effect     | Capability              | Approval                 |
|-------------------------|-----------|-----------------|-------------------------|--------------------------|
| `LoomLspGotoDefinition` | `Explore` | `None` / `Read` | `qyl.loom.lsp.navigate` | —                        |
| `LoomLspFindReferences` | `Explore` | `None` / `Read` | `qyl.loom.lsp.navigate` | —                        |
| `LoomLspSymbols`        | `Explore` | `None` / `Read` | `qyl.loom.lsp.navigate` | —                        |
| `LoomLspDiagnostics`    | `Detect`  | `None` / `Read` | `qyl.loom.lsp.diagnose` | —                        |
| `LoomLspPrepareRename`  | `Plan`    | `None` / `Read` | `qyl.loom.lsp.rename`   | —                        |
| `LoomLspRename`         | `Fix`     | `Write`         | `qyl.loom.lsp.rename`   | **`[RequiresApproval]`** |

Capability IDs use the `qyl.loom.*` prefix (no versioned segment — the previous `v2` subscope was retired along with the
stub `src/qyl.loom/V2/` directory).

## Execute-ready prompt

```
You are implementing LSP Phase 3 (Loom bridge) for qyl.

## Workspace
- Create a worktree: `git worktree add .worktrees/lsp-phase3 -b lsp-phase3 main`
- `cd .worktrees/lsp-phase3 && git status` (expect clean).

## Read first (absolute paths)
- /Users/ancplua/qyl/.claude/skills/qyl-lsp/SKILL.md
- /Users/ancplua/qyl/.claude/handoffs/2026-04-17-spec3-lsp-phase1.md (completed spec)
- /Users/ancplua/qyl/docs/planned/2026-04-18-lsp-phase3-loom-bridge.md (this file)
- /Users/ancplua/qyl/src/qyl.loom/CompilerDemo/LoomDemoWorkflow.cs (the exact attribute pattern to mirror: `[LoomStep]` + `[LoomTool]` + `[LoomContract]` + `[LoomWorkflow]`)
- /Users/ancplua/qyl/src/qyl.loom/Tools/LoomDetectors.cs (sibling location for the new `LoomLspTools.cs`)
- /Users/ancplua/qyl/src/qyl.mcp/Tools/Lsp/LspTools.cs (the MCP surface you're wrapping)

## Baseline
`dotnet build src/qyl.loom/qyl.loom.csproj --tl:off` must be green.

## Steps
1. Create `src/qyl.loom/Tools/LoomLspTools.cs` with 6 methods, each stacked with [LoomTool(name, Phase, Description, UseOnlyWhen, DoNotUseWhen)] + [RequiresCapability(id)] + [ToolSideEffect(...)] + [EmitsStructuredOutput(typeof(...))]. The rename method additionally gets [RequiresApproval]. Capability IDs from the table in the spec.
2. Reuse Phase-1 LspClientPool via DI — do NOT duplicate the LSP process management. Inject or resolve LspClientPool; if cross-project reference is missing, add a ProjectReference from qyl.loom → qyl.mcp OR extract the pool to a shared library (preferred: ProjectReference; extraction is a bigger refactor).
3. If you ship typed inputs/outputs, define the `[LoomContract]` records alongside `LoomLspTools.cs`. If Phase 1 ships markdown-only, wrap that for now.
4. Run `nuke Generate` — Loom's source generator should pick up the new [LoomTool] attributes and emit registry entries for the 6 tools.
5. Smoke test: add a test under `tests/qyl.loom.tests/` that invokes `LoomLspGotoDefinition` against a known symbol in a fixture workspace.

## Repo rules
MAF wins. No suppression. No git stash. UTF-8 BOM on new .cs. C# 14 preview.
Never hand-edit .g.cs. Commit each green step with conventional subject.
Never push. Never merge main.

## DoD
- Loom's generator output (LoomGeneratedRegistry.g.cs or similar) contains all 6 LoomLsp* entries.
- `LoomLspRename` is gated by [RequiresApproval] — Loom policy engine refuses invocation without explicit approval in the run state.
- Unit test: Loom workflow invokes LoomLspSymbols and gets back > 0 symbols against a fixture workspace.
- Build green across qyl.loom + tests/qyl.mcp.tests.
- Report: commits, generator output diff for LoomLsp* registration, approval-gate test evidence.
```

## DoD checklist

- [ ] 6 `[LoomTool]` entries registered in `LoomGeneratedRegistry`
- [ ] `LoomLspRename` attempt without approval → refused by policy
- [ ] `LoomLspRename` attempt with approval → applies edits, returns summary
- [ ] A Loom executor test exercises at least one LSP tool end-to-end
- [ ] No duplication of `LspClient` lifecycle logic — Phase 1's `LspClientPool` is reused

## Risks / non-goals

- **DI wiring** — Loom's `Program.cs` currently registers loom-specific services only. Adding `LspClientPool` to Loom's
  DI requires either a shared library or a ProjectReference. Path will dictate structure.
- **Approval integration** — the existing `LoomGovernancePolicy.Evaluate(...)` surface is the gate. If it doesn't know
  about `LspRename`, add it explicitly.
- **Do not** pre-specify TypeScript — this phase works with whatever languages Phase 1/2 shipped. If only Phase 1 is
  live, only `.cs` files are navigable, which is fine for Loom on backend code.
- **Workspace root** in Loom's server process: inherit `QYL_WORKSPACE_ROOT` or derive from the run context.
