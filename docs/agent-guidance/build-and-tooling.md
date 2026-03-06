# Build and tooling workflow

## Source of truth for operations

- Prefer Rider MCP tools for local discovery/editing (`mcp__rider__*`).
- Use shell commands only when tool equivalents are unavailable or explicitly faster.
- Keep changes scoped: `codex_best_in_slot` constraints and local project rules supersede defaults.

## Build/orchestration

- Primary orchestrator: `eng/build/` with `nuke` tasks.
- Common tasks:
  - `nuke Full` (full pipeline, frontend + codegen + verify)
  - `nuke Ci` (CI-equivalent pipeline)
  - `nuke Generate --force-generate` (TypeSpec-driven generation)
- Runtime verification tasks in this repo are intentionally NUKE-first:
  - Do not run raw `dotnet test` unless explicitly requested otherwise.
  - Do not run `nuke test`.
- For frontend checks use Playwright MCP when validation is UI-facing.

## Codegen and protocol governance

- TypeSpec is source-of-truth: `core/specs/*.tsp`.
- Avoid manual edits of generated artifacts (`src/qyl.protocol/*g.cs`, generated dashboard API types, generated OpenAPI files).
- Use the documented skills when available:
  - `agent-md-refactor` for instructions files
  - `nuke-build`, `run-tests`, `typspec-codegen`, `instrument-service`.

## Environment for local commands

- Backend baseline: `.NET 10.0`, `C# 14`, NuGet Central Package Management.
- Frontend baseline: Node tooling in `src/qyl.dashboard/` and `src/qyl.browser/`.
- DuckDB runtime is glibc-only in container context; avoid Alpine/musl assumptions.

## After edits

- Keep a tight loop: change + build command relevant to touched area + targeted diagnostics.
- Do not run broad audit sweeps unless asked in-task.
