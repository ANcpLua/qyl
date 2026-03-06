# Worktree Bootstrap

**Type:** Infrastructure-as-Prompt (one-shot setup)
**When:** Setting up or resetting the multi-agent worktree environment.
**Idempotent:** Yes — safe to run repeatedly.

## Prompt

```
You are setting up a persistent multi-agent worktree environment for Claude and Codex.

Objective:
Create and maintain 6 permanent git worktrees under `.worktrees/` for isolated agent execution.

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
2. Roslyn source generator — `src/qyl.servicedefaults.generator/` (7 incremental pipelines, compile-time interceptors)
3. Runtime instrumentation — `src/qyl.servicedefaults/`, `src/qyl.collector/` (OTel wiring, collector ingestion)

Required worktrees:

| Worktree | Branch | Role | Model | Scope |
|----------|--------|------|-------|-------|
| `.worktrees/agent-spark-collector` | `worktree/agent-spark-collector` | OTLP collector maintainer | Sonnet (high) | `src/qyl.collector` — ingestion, DuckDB batching, SSE streaming. Minimal diffs, never modify generators/schema. |
| `.worktrees/agent-spark-generator` | `worktree/agent-spark-generator` | Roslyn pipeline maintainer | Opus (max) | `src/qyl.servicedefaults.generator` — incremental pipelines, generator correctness, compile-time interception. Never touch SchemaGenerator.cs. |
| `.worktrees/agent-spark-schema` | `worktree/agent-spark-schema` | Schema generator maintainer | Opus (max) | `eng/build/SchemaGenerator.cs` — OpenAPI→C# generation, DuckDB DDL. Single-source-of-truth pipeline. |
| `.worktrees/agent-gpt54-investigation` | `worktree/agent-gpt54-investigation` | Cross-layer debugging | Opus (max) | All layers — incident investigation, telemetry tracing, root cause analysis, fault isolation. |
| `.worktrees/agent-gpt54-refactor` | `worktree/agent-gpt54-refactor` | Refactor engineer | Opus (max) | Cross-module cleanup, architecture improvements, multi-file changes, cross-cutting concerns. |
| `.worktrees/agent-gpt54-maintenance` | `worktree/agent-gpt54-maintenance` | Repository gardener | Sonnet (high) | Small fixes, comment cleanup, verification improvements, documentation accuracy. |

Branch policy:
- Use branch `worktree/<worktree-name>`. Reuse if exists, create from HEAD if not.
- Reuse existing correctly-registered worktree paths without failing.

Inside each worktree, create or update:
- `AGENTS.md` (canonical — includes 3 architecture layers, role/scope/focus/tasks/guardrails, model routing, verification commands: `dotnet build`, `dotnet test`)
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
