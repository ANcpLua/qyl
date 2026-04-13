# Proposal — Claude Code Skills for qyl Development

**Status:** Draft for review
**Date:** 2026-04-13
**Audience:** AI agents working ON qyl (contributors), not consumers using qyl from outside

## Filename Question

The actual skill spec file is **`SKILL.md`** (singular), not `SKILLS.md`. Confirmed by the existing pattern in this repo:

```
plugins/qyl-for-ai/skills/qyl-workflow/SKILL.md
plugins/qyl-for-ai/skills/qyl-fix-issues/SKILL.md
plugins/qyl-for-ai/skills/qyl-code-review/SKILL.md
plugins/qyl-for-ai/skills/qyl-setup-monitoring/SKILL.md
```

This file is a *proposal*, not a skill — it lives at `docs/Skills/` for review. Date-prefixed to match the `docs/plans/` and `docs/specs/` convention already in `qyl.slnx`.

## Two Senses of "Skill" in qyl — Don't Conflate

| Term | Where | What it is |
|------|-------|------------|
| **Claude Code skill** | `plugins/<plugin>/skills/<name>/SKILL.md` | Markdown content loaded into Claude's context when invoked. Static instructions. |
| **`QylSkillKind`** | `src/qyl.mcp/Skills/QylSkillKind.cs` | Runtime C# enum bucketing MCP function tools. Selected via `QYL_SKILLS` env var. 9 buckets: `Inspect, Health, Analytics, Agent, Build, Anomaly, Loom, Apps, Debug`. |

A *Claude Code skill* that scaffolds a tool emits code annotated with `[QylSkill(QylSkillKind.X)]` into the right runtime bucket. The two layers are orthogonal.

## Gap Analysis

The 4 existing skills in `plugins/qyl-for-ai/` are all **consumer-facing** — they help an external AI agent USE qyl (find issues, review PRs with telemetry context, set up monitoring, run a workflow). None help an agent **work on qyl itself**. The candidates below target that gap.

## Pain Points Found in the Code

Each cites a real path. Read once, fix many times.

| # | Pain | Evidence |
|---|------|----------|
| 1 | **4 Roslyn generators, identical pattern.** New tool / entity = touch generator + attribute + emitter. Forgetting one = silent miss at runtime. | `src/qyl.instrumentation.generators/`, `src/qyl.collector.storage.generators/`, `src/qyl.mcp.generators/ToolManifestGenerator.cs`, `src/Qyl.Agents.Generator/McpServerGenerator.cs` |
| 2 | **9 `QylSkillKind` buckets × ~30 tool files** in `qyl.mcp/Tools/`. Adding a tool needs the right bucket + `[QylSkillAttribute]` + `[McpServerToolType]` + correct subdirectory. The session reminder confirmed this gap with the missing LSP surface. | `src/qyl.mcp/Tools/{Analysis,Auth,Debug,Discovery,Errors,Intelligence,Logs,Management,Metrics,Sessions,Traces,Triage}/` + 23 top-level `*Tools.cs` |
| 3 | **TypeSpec cascade.** 20+ `.tsp` model files → `openapi.yaml` → `qyl.contracts` → `DuckDbInsertGenerator` → MCP tool → dashboard openapi-typescript types. Schema drift is silent until build breaks somewhere downstream. | `core/specs/models/*.tsp`, `core/openapi/openapi.yaml`, `src/qyl.contracts/`, `src/qyl.collector.storage.generators/DuckDbInsertGenerator.cs`, `src/qyl.dashboard/src/types/` |
| 4 | **Loom V1 vs V2 split.** `qyl.loom/CodeReview/`, `qyl.loom/Autofix/`, `qyl.loom/Agents/` are V1; `qyl.loom/V2/LoomV2*.cs` is V2. Migration is partial and risky. | `src/qyl.loom/{CodeReview,Autofix,Agents}/` vs `src/qyl.loom/V2/` |
| 5 | **Cross-plane Autofix duplication.** Autofix logic appears in `qyl.loom/Autofix/`, `qyl.collector/Autofix/`, and `qyl.mcp/Tools/AutofixMcpTools.cs`. Cross-project consistency is hand-maintained. | `src/qyl.loom/Autofix/`, `src/qyl.collector/Autofix/`, `src/qyl.mcp/Tools/AutofixMcpTools.cs` |
| 6 | **Dashboard ↔ collector type sync.** `qyl.dashboard` consumes types generated from `openapi.yaml`. Schema change without regen = TS compile error. No skill scaffolds the regen + ripple. | `src/qyl.dashboard/src/types/`, `core/openapi/openapi.yaml` |

## Proposed Skills (ranked by impact)

| # | Skill | Pains | Args | Effort | Value |
|---|-------|-------|------|--------|-------|
| 1 | **qyl-add-mcp-tool** | 1, 2 | `<ToolName> <QylSkillKind>` | high | High — most common contributor task |
| 2 | **qyl-add-lsp-surface** | 2 + LSP gap | `<operation>` (goto-definition, find-references, …) | high | High — directly unblocks Loom workflows |
| 3 | **qyl-bump-typespec** | 3, 6 | `<model.tsp>` | high | High — currently the most error-prone task |
| 4 | **qyl-add-storage-entity** | 1, 3 | `<EntityName>` | medium | Medium — gates `DuckDbInsertGenerator` ergonomics |
| 5 | **qyl-add-instrumentation** | 1 | `<ActivitySource> <operation>` | medium | Medium — semconv 1.40 compliance gate |
| 6 | **qyl-loom-v2-port** | 4 | `<V1-class>` | medium | Medium — finite work, decays as migration completes |
| 7 | **qyl-dashboard-component** | 6 | `<ComponentName>` | medium | Medium — Base UI / TanStack pattern enforcement |
| 8 | **qyl-trace** | runtime debug | `<trace-id>` | low | Low — quality-of-life over collector MCP |
| 9 | **qyl-genai-validate** | semconv | `<ActivitySource> <operation>` | low | Low — narrow but high precision |
| 10 | **qyl-bump-pkg** | CPM Rules A/B/G | `<pkg> <from> <to>` | low | Low — already covered by `dotnet-architecture-lint` skill in sibling repo, but a thin qyl-flavored wrapper is cheap |

## Recommended First Cut

Build skills **1, 2, 3** first. They target the three highest-leverage pain points (tool-registration cascade, LSP gap from today's session reminder, TypeSpec cascade). Defer 4–10 until the first three prove the contract. **Don't ship all ten upfront** — let usage data tell you which ones earn their slot in the catalog.

## File Layout — Two Options

**Option A: extend the existing plugin**

```
plugins/qyl-for-ai/skills/qyl-add-mcp-tool/SKILL.md
plugins/qyl-for-ai/skills/qyl-add-lsp-surface/SKILL.md
plugins/qyl-for-ai/skills/qyl-bump-typespec/SKILL.md
```

**Option B: separate contributor plugin**

```
plugins/qyl-dev/skills/qyl-add-mcp-tool/SKILL.md
plugins/qyl-dev/skills/qyl-add-lsp-surface/SKILL.md
plugins/qyl-dev/skills/qyl-bump-typespec/SKILL.md
```

**Recommendation: Option B.** The existing `plugins/qyl-for-ai/` is positioned for end-users adopting qyl as an observability platform. Mixing contributor scaffolding into the same plugin pollutes the consumer surface and forces users to see skills they will never invoke. A sibling `plugins/qyl-dev/` keeps the two audiences separated and lets `mcp.json` differ if the contributor skills need different MCP wiring.

## Sub-command vs Flat?

Reference: `plugins/elegance-pipeline/` uses `commands/{init,run,status}.md` to expose `/elegance-pipeline:init`, `/elegance-pipeline:run`, `/elegance-pipeline:status` alongside a top-level `skills/elegance-pipeline/SKILL.md`.

For qyl-dev, **prefer flat top-level skills** (`/qyl-add-mcp-tool`, `/qyl-bump-typespec`). Reasons:

- Each scaffolding task is independent — no shared workflow state, no init/run/status lifecycle.
- The elegance-pipeline pattern only earns its sub-commands because it has a real persistent state machine (scout → judge → planner → verifier → implementer). qyl scaffolding doesn't.
- Flat names are easier to discover via fuzzy match in the slash menu.

If skills 4–10 ever grow into a real pipeline (e.g., `qyl-add-mcp-tool` calls `qyl-bump-typespec` calls `qyl-add-storage-entity` automatically), revisit and consider a `/qyl-dev:*` family then.

## Invocation Mode

All proposed skills should **omit** `disable-model-invocation: true`. Reasoning:

- The frontmatter `description` will trigger Claude to auto-suggest the skill when a contributor says "add a new MCP tool" without typing `/qyl-add-mcp-tool` explicitly.
- Auto-invocation is the whole point of skills over plain slash commands. If we wanted explicit-only, plain `commands/*.md` would suffice.

## Open Questions (for reviewer)

1. Plugin placement: extend `qyl-for-ai` or new `qyl-dev`? (Recommendation: new.)
2. First cut size: 3 skills as proposed, or smaller (start with `qyl-add-mcp-tool` alone) or larger?
3. Does `qyl-add-lsp-surface` belong as its own skill or a `--lsp` flag on `qyl-add-mcp-tool`? (Recommendation: own skill — LSP is a 6-tool family with shared runtime stack per the `qyl-lsp` skill in the sibling repo.)
4. Should `qyl-trace` be a skill at all, or just rely on `mcp__plugin_qyl_qyl__qyl_get_trace` directly? (Recommendation: skill, because it bundles the "summarize critical path + GenAI spans + errors" prompt with the raw MCP call.)
5. Versioning: skills currently have no version pin. Should this proposal introduce a `version:` frontmatter field, or defer until a skill changes shape?

## What This Proposal Does NOT Include

- Actual `SKILL.md` content for any of the 10 candidates. Those come after this proposal is approved.
- Changes to existing `plugins/qyl-for-ai/` skills.
- Hook configuration. Skills run on demand; hooks are a separate enforcement layer.
- Migration of existing slash commands to skills.

## Next Step

Reviewer picks: Option A or B, first-cut size (1/3/all), and answers the 5 open questions. Then a follow-up PR creates the chosen skill files at the chosen path.
