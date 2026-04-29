---
name: loom
description: Top-level index and router across qyl Loom skills. Dispatches to three workflow skills (loom-fix-issues, loom-review-bot-pr, loom-autofix) and two feature-setup skills (qyl-otel-exporter-setup, loom-create-alert). Use when asked to fix a qyl issue, triage exceptions, review PR bot feedback, run the headless autofix pipeline, configure a qyl feature, or create an alert. Routes deterministically; asks one clarifying question when signals conflict.
license: Apache-2.0
role: router
---

# Loom Workflows

Debug production issues and keep code quality tight using qyl's observability surface. This page routes to the right sub-skill.

qyl is OTLP-native — there is no qyl vendor SDK to install. Apps instrument with OpenTelemetry and ship telemetry to the qyl collector. See `qyl-otel-exporter-setup` for the transport pipeline.

## How to Fetch Skills

Skills live under `.claude/skills/<skill-name>/SKILL.md` in this repo. Read them directly; do not summarise via tools that truncate.

## Start Here — Read This Before Doing Anything

**Do not skip this section.** Do not assume which workflow the user needs. Ask first.

1. If the user mentions **fixing errors, debugging exceptions, or investigating production issues** → `loom-fix-issues`
2. If the user mentions **qyl review-bot comments on a PR** (e.g. `qyl[bot]`, `qyl-review[bot]`) → `loom-review-bot-pr`
3. If the user asks Loom to **auto-generate a fix diff / run the headless autofix pipeline on an issue** → `loom-autofix`
4. If the user mentions **OpenTelemetry, OTel Collector, or pointing an app's telemetry at qyl** → `qyl-otel-exporter-setup`
5. If the user mentions **alerts, alert rules, error-rate / threshold / regression alerts** → `loom-create-alert`

When unclear, **ask the user** which workflow applies. Do not guess.

Programmatic equivalent: call MCP tool `loom_route(userRequest, signals?)` on the `qyl.loom` server. It returns the same routing deterministically, including a single clarifying question when signals conflict.

---

## Workflow Skills

| Use when | Skill | Path |
|---|---|---|
| Finding and fixing production issues — stack traces, breadcrumbs, event data | [`loom-fix-issues`](./loom-fix-issues/SKILL.md) | `loom-fix-issues/SKILL.md` |
| Resolving review-bot comments (`qyl[bot]`, `qyl-review[bot]`, or extra bots you pass in) | [`loom-review-bot-pr`](./loom-review-bot-pr/SKILL.md) | `loom-review-bot-pr/SKILL.md` |
| Headless autofix pipeline — fixability gate → context → hypothesis → diff → audit | [`loom-autofix`](./loom-autofix/SKILL.md) | `loom-autofix/SKILL.md` |
| OpenTelemetry Collector pipeline exporting OTLP into qyl | [`qyl-otel-exporter-setup`](./qyl-otel-exporter-setup/SKILL.md) | `qyl-otel-exporter-setup/SKILL.md` |
| Alert-rule creation via qyl's Alerts API | [`loom-create-alert`](./loom-create-alert/SKILL.md) | `loom-create-alert/SKILL.md` |

Each skill carries its own detection logic, prerequisites, and step-by-step instructions. Trust the skill — read it carefully and follow it. Do not improvise or take shortcuts.

---

## MCP Surface Behind the Router

| Tool | Purpose |
|---|---|
| `loom_route` | Deterministic routing; returns `Clarify` with one focused question when signals conflict |
| `loom_parse_review_bot_comments` | Deterministic parser for qyl review-bot PR comments |
| `loom_get_issue_insight`, `loom_start_fix_run`, `loom_generate_pr_review` | On `LoomGodAnalyzerServer` — pre-investigation insight, autofix run creation, qyl-produced PR review |

| Prompt | Purpose |
|---|---|
| `qyl.loom.route` | LLM-side router prompt — mirrors `loom_route` for chat-style agents |
| `qyl.loom.fix_issue` | 7-phase fix workflow with the untrusted-input posture |
| `qyl.loom.review_bot_pr` | Severity/confidence classification + fix + report |
| `qyl.loom.autofix_system` | Headless autofix pipeline driver prompt |

---

## Routing Hard Rules

- **Always route first.** Never pick a sub-skill from a keyword in the user's message — call `loom_route` (or follow the table above verbatim) and act on its decision.
- **Structured signals win.** If you already have `pullRequestNumber + reviewBotAuthor`, `issueId`, or `repoRoot`, pass them — the router skips keyword matching.
- **Two disjoint matches → clarify.** The router returns `Clarify` with one question when the request spans multiple workflows. Ask the user verbatim; do not guess.
