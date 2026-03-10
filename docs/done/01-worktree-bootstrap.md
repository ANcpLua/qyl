# Worktree Bootstrap

**Type:** Infrastructure-as-Prompt (one-shot setup)
**When:** Setting up or resetting the multi-agent worktree environment.
**Idempotent:** Yes — safe to run repeatedly.
**Canonical:** This is the single source of truth for worktree layout. No other doc competes.

## Prompt

```
You are setting up a persistent multi-agent worktree environment for Claude and Codex.

Objective:
Create and maintain 4 permanent git worktrees under `.worktrees/` for isolated agent execution.

Environment assumptions:
- Persistent machine or self-hosted CI runner (not ephemeral hosted CI).
- Use `bash`, `git`, and normal filesystem operations only.
- The setup process must not depend on model-specific tools or proprietary APIs.
- It is acceptable to create Claude- and Codex-specific instruction files as part of the worktree infrastructure.

Execution mode:
- Be opinionated and deterministic. Do not ask clarifying questions.
- Follow the exact layout and file contents requested. Do not improvise structure.
- Idempotent and safe to run multiple times. Fail fast on real errors.
- Skip already-correct worktrees without failing.
- If a path already exists but is not a valid matching worktree, report an error instead of overwriting.

Model routing policy:
- Default: Claude Sonnet 4.6 (effort: high)
- Escalation: Claude Opus 4.6 (effort: max)
- Opus for: Roslyn generators, analyzers, incremental pipelines, schema generation, multi-module refactors, cross-layer investigation, polyglot integration, deep root cause analysis
- Sonnet for: scoped single-worktree execution, small fixes, cleanup, docs, maintenance

Repository architecture (3 layers — never conflate):
1. Schema generation — `eng/build/SchemaGenerator.cs` (NUKE build step, emits models/enums/DDL)
2. Roslyn source generator — `src/qyl.instrumentation.generators/` (7 incremental pipelines, compile-time interceptors)
3. Runtime + collector — `src/qyl.servicedefaults/`, `src/qyl.collector/` (OTel wiring, collector discovery, DevLogs bridge, OTLP ingestion, DuckDB, SSE)

Required worktrees:

| Worktree | Branch | Ownership |
|----------|--------|-----------|
| `.worktrees/wt-backend` | `dev/wt-backend` | `src/qyl.collector`, API handlers, DuckDB queries, persistence, backend Loom services. Only touch `eng/build` if a backend contract bug is proven from generated schema/DDL. Do not touch `src/qyl.instrumentation.generators` unless a compile-time interceptor bug directly causes the failing backend behavior. |
| `.worktrees/wt-frontend` | `dev/wt-frontend` | `src/qyl.dashboard`. Never touch any generator layer. If UI failures come from bad backend shape, document the contract mismatch and hand to backend. |
| `.worktrees/wt-loom` | `dev/wt-loom` | Loom-specific services and verification. Treat generator and runtime layers as constrained deps. If Autofix/Triage/Regression needs missing telemetry, prove whether it's collector ingestion, generated instrumentation, or runtime wiring before editing shared infra. |
| `.worktrees/wt-mcp` | `dev/wt-mcp` | `src/qyl.mcp` and protocol exposure. Do not "fix" missing tool behavior by rewriting runtime instrumentation unless protocol validation proves root cause is telemetry availability. |

Branch policy:
- Use branch `dev/<worktree-name>`. Reuse if exists, create from HEAD if not.
- Reuse existing correctly-registered worktree paths without failing.

Verification:
- `dotnet build` only — do NOT run `dotnet test` or `nuke test` in worktrees.

Inside each worktree, create or update:
- `AGENTS.md` (canonical — includes 3 architecture layers, role/scope/ownership/guardrails, model routing)
- `CLAUDE.md` (mirrors AGENTS.md content)
- `.codex/agent.md` (mirrors AGENTS.md content)

Required output:
- Single idempotent bash script at `scripts/setup-worktrees.sh`
- Write the script and run it
- Print summary: created/reused/skipped worktrees, paths, branches, file confirmations, `git worktree list`

Constraints:
- Do not modify existing source code — only worktree infrastructure files.
- Do not overwrite conflicting non-worktree directories.
```
