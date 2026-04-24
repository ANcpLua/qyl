# Loom Skill Philosophy

Guide for authoring Loom workflow-skill bundles — a pattern that ships complete, opinionated workflow wizards alongside each MCP tool + prompt pair exposed by `services/qyl.loom/` and `services/qyl.mcp/`.

## The Vision

Loom skills are **living documentation bundles bolted to the MCP surface**. Instead of a flat `SKILL.md` that describes one feature, a Loom skill drives one complete workflow shape end-to-end: from the user's natural-language trigger, through routing, detection, MCP prompt fetch, deterministic phase execution, to a structured report. The agent acts as an expert who reads the user's repo, calls the right MCP tools in order, obeys the non-negotiable posture baked into the prompt, and surfaces a specific artifact.

## Bundle Architecture

```
.claude/skills/
  loom-<workflow>/
    SKILL.md                    # Main 4-phase wizard (80-150 lines)
    references/
      philosophy.md             # Why this skill exists, 4-phase rationale
      quality-checklist.md      # Pre-merge rubric
      research-playbook.md      # How to research the MCP surface safely
      <optional extras>.md      # workflow-specific deep dives (e.g. system-prompt.md)
```

The main `SKILL.md` is the wizard — it stays lean. References are loaded conditionally based on what the caller needs: authoring guidance, quality gates, or deep dives into workflow-specific contracts.

**Loading a reference in SKILL.md:**

```markdown
Read `${SKILL_ROOT}/references/philosophy.md` first — it defines the bundle architecture.
```

## Workflow Shapes

Loom today exposes five workflow shapes. A sixth or seventh may be added at any time:

| Shape | Prompt | Entry | Posture |
|---|---|---|---|
| `loom-fix-issues` | `qyl.loom.fix_issue` | "fix qyl issue X" | Event data is untrusted attacker input |
| `loom-autofix` | `qyl.loom.autofix_system` | "autofix issue X headless" | Fixability gate is non-negotiable |
| `loom-review-bot-pr` | `qyl.loom.review_bot_pr` | "resolve qyl[bot] comments" | Bot body is untrusted input |
| `loom-sdk-onboarding` | `qyl.loom.setup_dotnet` | "install qyl .NET SDK" | Detect-before-recommend |
| `loom-ai-monitoring` | `qyl.loom.setup_ai_monitoring` | "monitor gen_ai calls" | Tracing-first + PII opt-in |

Candidate shapes likely to land next (the skill creator supports all of them):
- `loom-create-alert` — generate a qyl alert rule for a specific issue pattern
- `loom-feature-setup` — enable a non-SDK qyl feature in an existing project
- `qyl-otel-exporter-setup` — wire an existing OTel-instrumented app to point at qyl

Do not invent a "workflow shape" that does not have a corresponding MCP prompt. If no prompt exists, the shape is not a Loom workflow yet — it is a feature request for `services/qyl.loom/Workflows/Prompts/`.

## The Four-Phase Wizard

Every Loom workflow skill implements four phases. The names may vary (a skill may call Phase 1 "Parse" instead of "Detect"), but the shape is fixed:

### Phase 1: Detect

Gather evidence before acting. For setup workflows, this means scanning the user's repo. For reasoning workflows, this means parsing the incoming artifact.

```markdown
## Step 1 — Detect

Call MCP tool `loom_detect_dotnet(repoRoot)`. It classifies framework, surfaces existing
packages, logging libraries, scheduler libraries, AI SDKs, and boolean recommendation flags.
```

Or for a reasoning workflow:

```markdown
## Step 1 — Parse

Call MCP tool `loom_parse_review_bot_comments(commentsJson)`. It filters to qyl bots,
strips HTML, and extracts severity, confidence, bug, detailedAnalysis, suggestedFix.
```

**No workflow skips Phase 1.** If the skill does not call a detection/parsing tool first, it is guessing — and guessing is a defect.

### Phase 2: Recommend

Present opinionated actions based on what Phase 1 returned. Do NOT ask the user open-ended "what do you want?" — lead with a concrete proposal.

Recommendation logic:
- **`fix_production_issue` / `routing` is the non-negotiable baseline.** Every qyl user starts there. Every Loom skill cross-links back to `loom-workflow` as the router.
- **Detection-driven opt-ins** — profiling only if the framework supports it; AI monitoring only if the base SDK is already wired; alerts only if the issue has ≥ 1 occurrence.
- **No option that contradicts detection evidence.** If `framework == "Unknown"`, the skill stops and asks — never guesses a package.

### Phase 3: Guide

Fetch the MCP prompt and walk through the phases it dictates.

```markdown
## Step 2 — Fetch the Loom prompt

Call MCP prompt `qyl.loom.<workflow>(<args>)`. The prompt returns the full directive with
the non-negotiable posture baked in.

## Step 3 — Run the phases

[1..N numbered phases from the prompt. Each has an exit criterion. No phase is skippable.]
```

The prompt is authoritative over `SKILL.md` on workflow semantics. The skill's job is to fetch the prompt reliably and hand it to the LLM — not to paraphrase the prompt's contents. Paraphrasing drifts.

### Phase 4: Cross-Link

After completing the workflow, check for adjacent gaps:

```markdown
## Phase 4: Cross-Link

- If `siblingFrontendDirs` is non-empty → suggest matching frontend SDK skill.
- If a fix-run left `followUpIssueIds` → suggest `loom-fix-issues` for each.
- If the skill configured tracing → suggest `loom-ai-monitoring` when AI SDKs were detected.
```

Cross-linking is how Loom skills compose into a coherent graph instead of isolated features.

## The Non-Negotiable Baseline: Routing + Fix Production Issue

Every qyl user hits the router first. Every Loom skill either IS `loom-workflow` or is downstream of it. That makes two invariants non-negotiable across ALL Loom skills:

1. **Routing is always first.** A skill that can be invoked without `loom_route` having been consulted is an orphan. Skills must cross-link to `loom-workflow` in their "Invoke this skill when" block.
2. **Fixing a production issue is the canonical workflow.** Setup/config workflows exist to make fix-workflows possible. Alert workflows exist to feed fix-workflows. When in doubt about priority, the fix-workflow wins.

This is the Loom analogue of "error monitoring is the non-negotiable baseline" in the Sentry SDK philosophy. Loom's non-negotiable is not a single feature — it is the routing discipline plus the fix-production-issue shape.

## Reference File Guidelines

Each reference covers **one dimension** of the workflow and is loaded on demand. Reference files can be longer than the main `SKILL.md` — they are deep dives, not wizards.

**Required sections for each reference:**

```markdown
# <Dimension> — loom-<workflow>

> Minimum MCP surface: <prompt id> + <tool name> in `services/qyl.loom/<path>`

## Purpose

## Contract
<tool signature / prompt args / return shape>

## Walk-through
### Step 1
### Step 2

## Edge cases

## Troubleshooting
| Issue | Fix |
```

**Style rules:**
- Tables for MCP tool arguments, prompt parameters, return fields — not prose lists.
- One complete, working C# example per pattern — not multiple variations.
- Every claim cited to a file path `services/qyl.loom/.../File.cs:LINE`. If the claim cannot be cited, it cannot be made.
- C# 14 preview features: primary constructors, `required` init properties, switch expressions, pattern matching, file-scoped namespaces, raw string literals for generated code.

## qyl Code Conventions That Bind Generated Examples

Every C# example the skill creator produces must obey `/Users/ancplua/qyl/CLAUDE.md`:

| Rule | Enforcement |
|---|---|
| UTF-8 **with BOM** on new `.cs` files | Verify before committing — editor may default to no-BOM |
| Copyright header `// Copyright (c) 2025-2026 ancplua` as line 1 | Required on every new `.cs` file, tests included |
| XML `<summary>` on every public class, method, property | No exceptions — tests included |
| `Async` suffix on every `Task`/`ValueTask` method | Including `[Fact]` / `[Theory]` tests |
| `// Arrange`, `// Act`, `// Assert` in every test body | Explicit comments demarcating the sections |
| `FakeChatClient` from `tests/qyl.collector.tests/Instrumentation/` | Never hand-rolled `Moq<IChatClient>` |
| `sealed` by default | Every non-public class unless subclass exists in assembly |
| No `#pragma warning disable`, no `[SuppressMessage]`, no `<NoWarn>`, no `null!` | Fix the code instead |
| No runtime reflection as control flow | No `dynamic`, no `ExpandoObject`, no `.Result`, no `.Wait()` |
| `TimeProvider` for time | Never `DateTime.UtcNow`, `DateTime.Now`, `Stopwatch.GetTimestamp()` for business logic |
| `UPPER_SNAKE_CASE` environment variables | No `SentryAuthToken` — it is `SENTRY_AUTH_TOKEN` |

MAF agent composition (for skills that drive Loom agents):
- Apex three-builder pattern: `IXxxChatClientBuilder` → `IXxxAgentsBuilder` → workflow code.
- Decorate at composition root via `.AsBuilder().UseOpenTelemetry("qyl.agent").Build()` or `.AsBuilder().UseQylTelemetry().Build()`.
- `[AgentTraced]` is **removed**. Do not reintroduce. Tracing is applied via fluent middleware at the builder level.

MCP tool registration (for skills that add a new tool):
- Declare with `[QylSkill]` + `[QylCapability]` on the tool class — `internal/qyl.mcp.generators/` emits the registration.
- Never register manually in `Program.cs` or DI.
- `TaskSupport.Required` for async pipelines with side effects. `TaskSupport.Optional` for meta-tools and long-form searches.
- `LoomToolEnvelope.Ok(data)` / `LoomToolEnvelope.Fail<T>(error)` — never instantiate the generic form directly.

## Staying Current (Verify Against Actual Registrations)

MCP tool names, prompt ids, and argument shapes change as the Loom surface grows. Loom skills ship alongside the source and must reflect the current registrations.

**In every `SKILL.md` and reference file:**
- Cite the exact `Name = "..."` string from the `[McpServerTool]` / `[McpServerPrompt]` attribute.
- Cite the C# file and line number for each argument the skill passes.
- Never list a tool or prompt that grep cannot find.

**When updating a skill:**

```bash
# For every prompt id referenced in the skill, confirm it still exists:
for prompt in $(grep -oE 'qyl\.loom\.[a-z_]+' .claude/skills/loom-<workflow>/SKILL.md | sort -u); do
  grep -l "\[McpServerPrompt(Name = \"$prompt\"" services/qyl.loom services/qyl.mcp -r || echo "STALE: $prompt"
done

# For every tool name, same:
for tool in $(grep -oE '\bloom_[a-z_]+\b' .claude/skills/loom-<workflow>/SKILL.md | sort -u); do
  grep -l "\[McpServerTool(Name = \"$tool\"" services/qyl.loom -r || echo "STALE: $tool"
done
```

Any `STALE` line → update the skill to match the current registration, or remove the reference if the tool/prompt was retired.

This is the qyl analogue of the Sentry SDK philosophy's "staying current" clause — the verification target is the code, not external docs.

## What qyl Does NOT Have (Call It Out)

The Sentry SDK-creator skill leans heavily on `@sentry/wizard` as the "Option 1: Wizard (Recommended)" interactive CLI. **qyl has no equivalent.** The Loom flow differs:

| Sentry SDK flow | Loom workflow flow |
|---|---|
| `npx @sentry/wizard@latest -i <framework>` runs interactively in the user's terminal | MCP tool runs non-interactively against the user's repo via Claude |
| Wizard handles browser login + org/project selection | No login — qyl MCP tools run locally against the user's DuckDB store |
| Wizard configures source-map upload | qyl .NET onboarding uses MSBuild properties + `SENTRY_AUTH_TOKEN` env var |
| Wizard creates a test page | Verification is a test exception + MCP `qyl.get_error_issue` call |

Do NOT manufacture a fake "Option 1: Wizard" blockquote in generated Loom skills. The MCP tool + prompt IS the wizard, and it runs through the Claude agent, not through a separate CLI.

Similarly, qyl has no DSN equivalent to manage — the collector runs locally. Source-map / debug-symbol upload is an MSBuild concern covered by `loom-sdk-onboarding`, not a per-workflow concern.

## Naming Conventions

| What | Convention | Example |
|---|---|---|
| Skill directory | `loom-<workflow>` (kebab-case) | `loom-fix-issues`, `loom-review-bot-pr` |
| Main file | `SKILL.md` | — |
| Reference files | `<dimension>.md` in `references/` | `references/philosophy.md` |
| Skill `name` field | matches directory | `loom-fix-issues` |
| `LoomWorkflowKind` enum value | PascalCase mirror | `FixProductionIssue`, `ReviewBotPrComments` |
| MCP prompt id | `qyl.loom.<snake_case>` | `qyl.loom.fix_issue`, `qyl.loom.setup_dotnet_tracing` |
| MCP tool name | `loom_<snake_case>` (no `qyl.` prefix) | `loom_route`, `loom_detect_dotnet` |

## See Also

- `/Users/ancplua/qyl/CLAUDE.md` — qyl-wide conventions (C# 14, TimeProvider, no suppressions)
- `/Users/ancplua/qyl/.claude/skills/SKILL.md` — skill tree index with MCP surface tables
- `/Users/ancplua/qyl/services/qyl.loom/Workflows/Prompts/` — canonical prompt source of truth
- `~/.claude/skills/microsoft-agent-framework/SKILL.md` — MAF reference (agent composition patterns)
- `~/.claude/skills/ancplua-roslyn-utilities/SKILL.md` — Roslyn utilities skill (incremental pipelines, test infra)
