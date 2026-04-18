# PLANNED — LSP Phase 2: TypeScript support

**Status:** Planned. Phase 1 shipped on 2026-04-17 in `main` (`csharp-ls` only).
**Predecessor:** `.claude/handoffs/2026-04-17-spec3-lsp-phase1.md` (completed).
**Unlocks:** Loom can navigate `src/qyl.dashboard/` TypeScript/React code the same way it navigates C#.

## Outcome

```
QYL_SKILLS=debug qyl-mcp
# Then, from Claude Code:
#   qyl.lsp.goto_definition filePath=src/qyl.dashboard/src/App.tsx line=12 column=3
#   → lands on the symbol's definition in the TS file
```

All 6 LSP tools (`lsp_goto_definition`, `lsp_find_references`, `lsp_symbols`, `lsp_diagnostics`, `lsp_prepare_rename`, `lsp_rename`) work against `.ts`, `.tsx`, `.js`, `.jsx` files in addition to `.cs`.

## Scope

Extend (do **not** add new tools — same 6 tools, wider language coverage):

- `src/qyl.mcp/Tools/Lsp/LspLanguageMappings.cs` — add extension → serverId entries
- `src/qyl.mcp/Tools/Lsp/LspServerDefinitions.cs` — add `typescript-language-server` definition
- `src/qyl.mcp/Tools/Lsp/LspServerInstallation.cs` — install-command hint (`npm i -g typescript-language-server typescript`)
- `src/qyl.mcp/Tools/Lsp/LspServerResolution.cs` — workspace-root detection: walk up from target file looking for `tsconfig.json` or `package.json`; fall back to `QYL_WORKSPACE_ROOT`

Do **not** touch the process/transport/client/wrapper layers — the runtime stack is language-agnostic by design in Phase 1.

## Prerequisites

- `typescript-language-server --stdio` must launch and speak LSP over stdio. Binary path discovered via `which typescript-language-server` (from global `npm i -g`) or from a project-local `node_modules/.bin`.
- TypeScript itself (`tsc`) must be available in the workspace for diagnostics to work — already the case for `src/qyl.dashboard/` since it's a Vite project.

## Execute-ready prompt

```
You are implementing LSP Phase 2 (TypeScript support) for qyl.mcp.

## Workspace
- Primary repo: /Users/ancplua/qyl on branch main at the tip that has Phase 1.
- Create a worktree: `git worktree add .worktrees/lsp-phase2 -b lsp-phase2 main`
- `cd .worktrees/lsp-phase2 && git status` (expect clean).

## Read first (absolute paths)
- /Users/ancplua/qyl/.claude/skills/qyl-lsp/SKILL.md — runtime-stack source of truth
- /Users/ancplua/qyl/docs/planned/2026-04-18-lsp-phase2-typescript.md (this file)

## Baseline
`dotnet build src/qyl.mcp/qyl.mcp.csproj --tl:off` must be green BEFORE edits. If not, STOP.

## Pre-flight
`npm i -g typescript-language-server typescript` and verify `which typescript-language-server` resolves.
If the machine can't install npm globals, STOP and report — do not fake-skip.

## Steps
1. LspServerDefinitions.cs: add `typescript-language-server` server with args `["--stdio"]`, bin name `typescript-language-server`, install hint string.
2. LspLanguageMappings.cs: map `.ts`, `.tsx`, `.js`, `.jsx` → the new serverId.
3. LspServerResolution.cs: walk up from target file looking for `tsconfig.json` (preferred) or `package.json` (fallback). If neither found inside `QYL_WORKSPACE_ROOT`, use QYL_WORKSPACE_ROOT itself. Relative paths still rejected.
4. Build green after each step. Commit each with `feat(mcp): lsp phase2 <layer>` subjects.
5. Live smoke: call `qyl.lsp.symbols query=Dashboard scope=workspace` against src/qyl.dashboard/; expect ≥ 1 hit.

## Repo rules
MAF wins. No suppression. No git stash. UTF-8 BOM on new .cs. C# 14 preview.
Never hand-edit .g.cs. Commit after each green step with conventional-commit subject.
Never push. Never merge main.

## DoD
- All 6 LSP tools work on a .tsx file (smoke test: find a React component, goto_definition, find_references, symbols query, diagnostics, prepare_rename, rename).
- Build green across qyl.mcp + tests/qyl.mcp.tests.
- `which csharp-ls && which typescript-language-server` both resolve — mixed-language workspace works.
- Report: commits (`git log --oneline lsp-phase2 ^main`), smoke results, any divergence.
```

## DoD checklist

- [ ] `typescript-language-server` launches from the pool for `.tsx` files
- [ ] `lsp_goto_definition` on a React component import resolves correctly
- [ ] `lsp_diagnostics` on a broken `.tsx` file returns TS diagnostics
- [ ] C# workspace and TS workspace coexist (two `LspClient` instances in the pool, keyed independently)
- [ ] Binary-missing error message includes the install command (`npm i -g typescript-language-server typescript`)

## Risks / non-goals

- **Cold start:** `typescript-language-server` indexes faster than `csharp-ls` (~2 s vs ~8 s on this repo) but budget 30 s for the first request against a large TS workspace.
- **Razor / `.cshtml` / Vue** — out of scope. Add when a user actually asks.
- **Workspace roots:** one client per (serverId, workspaceRoot). A monorepo with multiple `tsconfig.json` boundaries spawns multiple servers. Acceptable.
- **Do not** add a new `QylSkillKind` — still reuses `Debug` (opt-in via `QYL_SKILLS=debug`).
