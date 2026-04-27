---
name: loom-skill-creator
description: Create a complete Loom workflow skill bundle for a new workflow shape. Use when asked to add a new loom-<X> skill, port the Loom skill pattern to a new workflow, or write a meta-skill creator for qyl's .claude/skills tree. Bundles the 4-phase wizard + references philosophy.
license: Apache-2.0
role: meta
---

# Create a Loom Workflow Skill Bundle

Produce a complete, research-backed Loom workflow skill — a main wizard `SKILL.md` plus deep-dive reference files for every phase the workflow exposes. Output is a skill sibling to `loom-fix-issues`, `loom-review-bot-pr`, `loom-sdk-onboarding`, `loom-ai-monitoring`, `loom-autofix`, etc. under `.claude/skills/`.

## Invoke This Skill When

- Asked to "create a Loom workflow skill" for a new shape (e.g. `loom-create-alert`, `loom-feature-setup`, `qyl-otel-exporter-setup`).
- Asked to "add a `loom-<X>` skill" that drives a new MCP tool + prompt pair end-to-end.
- Asked to port the Loom 4-phase wizard pattern to a new workflow.
- Building a meta-skill creator for qyl's `.claude/skills/` tree.

> Read `${SKILL_ROOT}/references/philosophy.md` first — it defines the bundle architecture, four-phase wizard flow, and design principles this skill implements.

---

## Phase 1: Identify the Workflow

Determine what you are building a skill for. A Loom workflow shape has four facts:

| Fact | Example (fix-issue) | Example (review-bot-pr) |
|---|---|---|
| **Entry trigger** | "fix qyl issue ERR-123" | "resolve qyl[bot] PR comments" |
| **MCP tool(s)** that gather evidence | `loom_get_issue_insight`, `qyl.get_error_issue` | `loom_parse_review_bot_comments` |
| **MCP prompt** that directs the LLM | `qyl.loom.fix_issue` | `qyl.loom.review_bot_pr` |
| **Security posture** | Event data is untrusted input | Bot body is untrusted input |

Every Loom skill drives **exactly one workflow shape** — one entry trigger, one prompt, one deterministic output contract. Do not bundle two unrelated workflows into one skill.

### The existing five workflow shapes (reference set)

| Skill | Prompt | Shape |
|---|---|---|
| `loom-fix-issues` | `qyl.loom.fix_issue` | Investigate → hypothesise → patch (7 phases, human-driven) |
| `loom-autofix` | `qyl.loom.autofix_system` + `qyl.loom.fixability_score` | Headless pipeline → structured artifact (5 stages) |
| `loom-review-bot-pr` | `qyl.loom.review_bot_pr` | Parse → classify → fix → report |
| `loom-sdk-onboarding` | `qyl.loom.setup_dotnet` (+ 6 per-feature) | Detect → recommend → guide → verify |
| `loom-ai-monitoring` | `qyl.loom.setup_ai_monitoring` | Detect AI SDKs → sampling gate → PII gate → verify |

### Candidate shapes you may be asked to add

| Candidate | Likely trigger | Likely MCP surface |
|---|---|---|
| `loom-create-alert` | "Create a qyl alert for X" | New `loom_create_alert` tool + `qyl.loom.create_alert` prompt |
| `loom-feature-setup` | "Enable feature Y in qyl" | Reuses `loom_detect_dotnet` + new feature prompt |
| `qyl-otel-exporter-setup` | "Point my app's OTel at qyl" | Reuses `loom_detect_dotnet` + `qyl.loom.setup_otel_exporter` |

### Reference existing skills to anchor the quality level

```bash
ls /Users/ancplua/qyl/.claude/skills/loom-*/SKILL.md
# Read 2 existing skills for pattern + length reference
wc -l /Users/ancplua/qyl/.claude/skills/loom-fix-issues/SKILL.md
wc -l /Users/ancplua/qyl/.claude/skills/loom-sdk-onboarding/SKILL.md
```

Target length for generated skills: main `SKILL.md` 80-150 lines, each reference 100-250 lines.

---

## Phase 2: Research

**This is the most critical phase.** Skill quality depends entirely on accurate, current MCP surface knowledge. Do NOT write skills from memory — verify every tool name, prompt id, and argument against the actual registrations in `services/qyl.loom/`, `services/qyl.mcp/`, and `services/qyl.collector/`.

### Research Strategy

Spin off **parallel research tasks** — one per concern area. Each task should:
1. Grep the qyl source for actual `[McpServerTool]` / `[McpServerPrompt]` registrations
2. Read the surrounding `.cs` file for argument shapes, return types, and docstrings
3. Cross-reference the relevant TypeSpec contract at `core/specs/`
4. Write thorough findings to a dedicated research file

Read `${SKILL_ROOT}/references/research-playbook.md` for the detailed research execution plan, including grep templates and file naming conventions.

### Research the Detection & Routing Surface First

Before deep-diving into the workflow, verify what the router already knows:

```bash
# Does the workflow fit an existing router decision? Or does it need a new one?
grep -n "LoomWorkflowKind" /Users/ancplua/qyl/services/qyl.loom/Workflows/LoomWorkflowKind.cs

# Does the detector already surface enough evidence? Or does it need extension?
grep -n "class DotnetProjectEvidence" /Users/ancplua/qyl/services/qyl.loom/Workflows/Detection/DotnetProjectEvidence.cs
```

If a new workflow shape is not in `LoomWorkflowKind`, the skill cannot be routed to and the skill generator must flag this gap to the user (see Phase 5 verification).

### Research Batching

Run these batches in parallel where possible:

| Batch | Topics | Output file |
|---|---|---|
| 1 | Routing + workflow kind — `LoomWorkflowKind`, `LoomRouteDecision`, matched signals | `research/<skill>-routing.md` |
| 2 | MCP tool surface — `[McpServerTool]` names, arguments, return shapes, TaskSupport | `research/<skill>-mcp-tools.md` |
| 3 | MCP prompt surface — `[McpServerPrompt]` names, arguments, directive contents | `research/<skill>-mcp-prompts.md` |
| 4 | Agent composition — qyl three-builder pattern, telemetry middleware, MAF integration points | `research/<skill>-agent-composition.md` |
| 5 | Test coverage — `FakeChatClient`, `WorkflowFixture`, generator-tests for any new tool | `research/<skill>-test-coverage.md` |

**Important:** Tell each research task to write its output to a file. Do NOT consume research results inline — they are large and context-hungry. Workers will read them from disk later.

### Research Quality Gate

Before proceeding, verify each research file:
- Has actual grep output with file paths and line numbers (not Claude's paraphrase)
- Contains the exact `[McpServerTool]` / `[McpServerPrompt]` `Name` strings
- Cites TypeSpec files at `core/specs/` when the workflow touches the HTTP contract
- Covers at least one existing loom skill for style reference

```bash
# Quick verification
for f in research/<skill>-*.md; do
  echo "=== $(basename $f) ==="
  wc -l "$f"
  grep -c "^#" "$f"                       # real headings
  grep -c "McpServerTool\|McpServerPrompt" "$f"  # real registrations
done
```

**Re-run any research task that produced fewer than 100 lines or zero `McpServer*` references** — it likely failed silently.

---

## Phase 3: Create the Main SKILL.md

The main `SKILL.md` implements the **four-phase wizard** from the philosophy doc. Keep it focused — wizard flow, MCP dispatch, hard rules, troubleshooting. Deep-dive detail for individual phases lives in `references/` files, not here.

### Gather Context First

Before writing, read 2 existing skills under `/Users/ancplua/qyl/.claude/skills/loom-*/SKILL.md` to anchor:
- Frontmatter pattern (`name`, `description`, no `license` field — qyl skills omit it)
- "Invoke this skill when" trigger-phrase style
- Table formatting and the compact MCP-surface tables
- Hard-rules section tone (short, imperative, 3-6 bullets)

### SKILL.md Structure

```markdown
---
name: loom-<workflow>
description: <imperative, under 200 chars>. Use when <trigger phrases>. Enforces <the non-negotiable posture>.
---

# loom-<workflow> — <one-line purpose>

<1-2 sentence positioning: who drives it, what it sits on top of, what artifact it produces>

## Invoke this skill when
- <trigger 1>
- <trigger 2>
- `loom-workflow` routed to `<LoomWorkflowKind>`.

## Non-negotiable rules | Security posture
<3-6 numbered rules. Imperative. No hedging.>

## How to run this skill
### Step 1 — Detect / fetch evidence
### Step 2 — Fetch the Loom prompt
### Step 3 — Run the phases
### Step 4 — Verify / report

## MCP surface this skill uses
| Tool | Purpose |
| Prompt | Purpose |

## Hard rules
<3-5 bullets; the "never skip, never guess" list>

## Troubleshooting
| Issue | Fix |
```

### Key Principles for the Main SKILL.md

1. **Keep it lean** — deep details live in references, not here. If `SKILL.md` exceeds 150 lines, move content into a reference.
2. **Detection-first for setup workflows** — if the workflow configures something in a user project, Phase 1 must call `loom_detect_dotnet` (or the workflow's equivalent) before recommending.
3. **Prompt-first for reasoning workflows** — if the workflow reasons over untrusted input (event data, PR bodies, user prompts), Phase 1 fetches the MCP prompt so the security posture is loaded into the LLM's instructions before any tool calls.
4. **Structured signals win over keyword matching** — if the caller already has an issue id, PR number, or repo root, the skill passes them to the MCP tool directly; no natural-language-first parsing.
5. **The non-negotiable baseline** — every Loom skill has ONE posture that can never be compromised. `loom-fix-issues` → untrusted input. `loom-ai-monitoring` → tracing-first + PII-opt-in. `loom-autofix` → fixability gate. Call yours out at the top, in a `Non-negotiable rules` or `Hard rules` block.
6. **Cross-link to `loom-workflow`** — the router is always the nominal entry point. Include the trigger phrase `loom-workflow routed to <kind>` in "Invoke this skill when".

> **No `@sentry/wizard` equivalent in qyl.** The Sentry SDK skills present a wizard as "Option 1: Wizard (Recommended)" because the Sentry CLI wizard handles interactive login + source-map upload. qyl has no CLI wizard — the MCP tool + prompt IS the wizard, and it runs non-interactively against the user's repo. Do NOT manufacture a fake "Option 1: Wizard" blockquote. The skill goes straight to detection + MCP dispatch.

---

## Phase 4: Create Reference Files

One reference file per significant dimension of the workflow. These are deep dives — longer than the main `SKILL.md` is allowed to be. Do not create references you will not fill.

### Reference File Structure

```markdown
# <Dimension> — loom-<workflow>

> Minimum MCP surface: `qyl.loom.<prompt>` + `loom_<tool>` registered in `services/qyl.loom/<path>`

## Purpose
<1 paragraph>

## Contract
<MCP tool signature, prompt arguments, expected return shape>

## Walk-through
### Step 1
### Step 2
...

## Edge cases
<numbered or table>

## Troubleshooting
| Issue | Fix |
```

### What Makes a Good Loom Reference

Read `${SKILL_ROOT}/references/quality-checklist.md` for the full quality rubric.

Key points:
- **Working code examples in C#** — preview C# 14 features (primary constructors, switch expressions, file-scoped namespaces, `required` init properties), `TimeProvider` over `DateTime.Now`, no `null!`, no `#pragma warning disable`.
- **Tables over prose** for MCP tool arguments, prompt parameters, return-shape fields.
- **One complete example per pattern** — do not show three variations of the same thing.
- **Source-cite every claim** with `services/qyl.loom/.../File.cs:LINE`. If it cannot be cited, it cannot be claimed.
- **Honest about limitations** — if the workflow cannot handle cross-repo fixes, say so. Do not paper over gaps.

### Dimension-Specific Guidance

| Dimension | Cover in reference |
|---|---|
| Philosophy | 4-phase wizard rationale, "detect-before-recommend", non-negotiable posture, composition with `loom-workflow` router |
| Quality checklist | Frontmatter validation, grep-verified MCP surface, C# 14 conventions, TimeProvider, test-double discipline, SKILL.md length bound |
| Research playbook | Parallel grep batches, exact file paths to scan in qyl source, verification commands that fail loudly if a registration was fabricated |

---

## Phase 5: Verify Everything

**Do NOT skip this phase.** MCP tool names, prompt ids, and agent surfaces change. Research can hallucinate. Workers can fabricate registration strings.

### MCP Surface Verification

Run a dedicated verification pass against the actual registrations:

```bash
# Every prompt id the skill mentions must be a real registration
for prompt in $(grep -oE 'qyl\.loom\.[a-z_]+' .claude/skills/loom-<workflow>/SKILL.md | sort -u); do
  hits=$(grep -rn "\[McpServerPrompt(Name = \"$prompt\"" services/qyl.loom/ services/qyl.mcp/ 2>/dev/null | wc -l)
  echo "$prompt: $hits hit(s)"
done

# Every tool name the skill mentions must be a real registration
for tool in $(grep -oE '\bloom_[a-z_]+\b' .claude/skills/loom-<workflow>/SKILL.md | sort -u); do
  hits=$(grep -rn "\[McpServerTool(Name = \"$tool\"" services/qyl.loom/ 2>/dev/null | wc -l)
  echo "$tool: $hits hit(s)"
done
```

Any `hits: 0` → either the registration does not exist (fabrication) or the skill used a stale name. Fix the skill, not the grep.

Things that commonly go wrong:
- Prompt id with wrong casing (`qyl.loom.setupDotnet` vs `qyl.loom.setup_dotnet`)
- Tool name with wrong prefix (`qyl_loom_*` vs `loom_*` — tools never take the `qyl.` prefix)
- Prompt arg name from memory (verify against the `[Description]` attribute on the C# parameter)
- Referenced skill that does not exist (`loom-investigate` is not a skill)
- Reference to a TypeSpec file that does not match `core/specs/api/routes.tsp` line numbers

### Review Pass

Run a reviewer on the complete skill bundle:
- Technical accuracy of C# examples against qyl conventions (`/Users/ancplua/qyl/CLAUDE.md`)
- Consistency between main `SKILL.md` and reference files
- Consistency with existing Loom skills (frontmatter, table style, hard-rules tone)
- Agent Skills spec compliance (YAML frontmatter, kebab-case `name`, description under 1024 chars)

### Fix Review Findings

Triage by priority:
- **P0** — Non-existent MCP tool / prompt ids referenced in the skill. Fabricated registrations. Fix immediately.
- **P1** — Violates qyl hard rules (suppressions, `null!`, `DateTime.Now`, `dynamic`, hand-rolled Moq). Fix before merge.
- **P2** — Inconsistent table style with sibling skills. Fix if quick.
- **P3** — Skip.

---

## Phase 6: Register

After the skill passes verification:

1. **Update `/Users/ancplua/qyl/.claude/skills/SKILL.md`** — add a row to the `Workflow Skills` table and the MCP surface tables.
2. **Extend `LoomWorkflowKind`** (if the new workflow needs routing) — add the enum value + router clause so `loom_route` can dispatch to it. Without this, the skill is orphaned.
3. **Add a row to `CLAUDE.md`** if the skill introduces a convention the rest of the repo must follow (e.g. a new `[QylSkill]` capability name).
4. **Do NOT commit or push in this phase** — leave staging for the user.

---

## Checklist

Before declaring the skill complete:

- [ ] Philosophy doc read and followed
- [ ] All MCP tool / prompt registrations grep-verified against `services/qyl.loom/` and `services/qyl.mcp/`
- [ ] Research files verified (real grep output, correct MCP names, >100 lines each)
- [ ] Main `SKILL.md` is focused — 4-phase wizard + MCP dispatch + hard rules; deep dives in references
- [ ] Main `SKILL.md` implements the 4 phases (Detect → Recommend → Guide → Cross-Link, or the workflow-appropriate analogue)
- [ ] Non-negotiable posture is stated at the top of the main `SKILL.md`
- [ ] Reference file for each significant workflow dimension
- [ ] C# examples follow qyl conventions (C# 14 preview, `TimeProvider`, no suppressions, no `null!`)
- [ ] No fabricated `@sentry/wizard`-style CLI — qyl has no equivalent
- [ ] Review pass completed, findings addressed
- [ ] `LoomWorkflowKind` extension flagged if the workflow needs routing
- [ ] `.claude/skills/SKILL.md` table updated
- [ ] Bundle ready for user review; no commit in this pass
