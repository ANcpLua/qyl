---
name: loom-workflow
description: Fix production issues, onboard .NET telemetry, and resolve qyl review-bot PR comments. Use when asked to fix a qyl issue, triage exceptions, review PR bot feedback, install a .NET SDK for qyl, or wire AI monitoring. Routes to the correct sub-skill; asks one clarifying question when signals conflict.
license: Apache-2.0
role: router
---

# Loom Workflows

Debug production issues, set up .NET telemetry, and keep code quality tight using qyl's observability surface. This page routes to the right sub-skill.

## How to Fetch Skills

Skills live under `.claude/skills/<skill-name>/SKILL.md` in this repo. Read them directly; do not summarise via tools that truncate.

## Start Here — Read This Before Doing Anything

**Do not skip this section.** Do not assume which workflow the user needs. Ask first.

1. If the user mentions **fixing errors, debugging exceptions, or investigating production issues** → `loom-fix-issues`
2. If the user mentions **qyl review-bot comments on a PR** (e.g. `qyl[bot]`, `qyl-review[bot]`) → `loom-review-bot-pr`
3. If the user mentions **installing a .NET telemetry SDK, wiring error / tracing / profiling / logging / metrics / crons** → `loom-sdk-onboarding`
4. If the user mentions **monitoring LLM calls, `gen_ai.*` spans, token usage, or AI-agent observability** → `loom-ai-monitoring`

When unclear, **ask the user** which of the four workflows applies. Do not guess.

Programmatic equivalent: call MCP tool `loom_route(userRequest, signals?)` on the `qyl.loom` server. It returns the same routing deterministically, including a single clarifying question when signals conflict.

---

## Workflow Skills

| Use when | Skill | Path |
|---|---|---|
| Finding and fixing production issues — stack traces, breadcrumbs, event data | [`loom-fix-issues`](./loom-fix-issues/SKILL.md) | `loom-fix-issues/SKILL.md` |
| Resolving review-bot comments (`qyl[bot]`, `qyl-review[bot]`, or extra bots you pass in) | [`loom-review-bot-pr`](./loom-review-bot-pr/SKILL.md) | `loom-review-bot-pr/SKILL.md` |
| Detection-first .NET telemetry SDK setup — error, tracing, profiling, logging, metrics, crons | [`loom-sdk-onboarding`](./loom-sdk-onboarding/SKILL.md) | `loom-sdk-onboarding/SKILL.md` |
| Wiring AI agent monitoring (`gen_ai.*`) — tracing-first, sampling gate, PII opt-in | [`loom-ai-monitoring`](./loom-ai-monitoring/SKILL.md) | `loom-ai-monitoring/SKILL.md` |

Each skill carries its own detection logic, prerequisites, and step-by-step instructions. Trust the skill — read it carefully and follow it. Do not improvise or take shortcuts.

---

## MCP Surface Behind the Router

| Tool | Purpose |
|---|---|
| `loom_route` | Deterministic routing; returns `Clarify` with one focused question when signals conflict |
| `loom_plan_task` | One-shot: route + detect (for onboarding workflows) or route + parse (for PR-review workflow) |
| `loom_detect_dotnet` | Scan a repo, classify .NET framework, surface Sentry/logging/scheduler/AI-SDK evidence |
| `loom_parse_review_bot_comments` | Deterministic parser for qyl review-bot PR comments |
| `loom_get_issue_insight`, `loom_start_fix_run`, `loom_review_pull_request` | On `LoomGodAnalyzerServer` — pre-investigation insight, autofix run creation, qyl-produced PR review |

| Prompt | Purpose |
|---|---|
| `qyl.loom.route` | LLM-side router prompt — mirrors `loom_route` for chat-style agents |
| `qyl.loom.fix_issue` | 7-phase fix workflow with the untrusted-input posture |
| `qyl.loom.review_bot_pr` | Severity/confidence classification + fix + report |
| `qyl.loom.setup_dotnet*` (7 prompts) | SDK onboarding wizard + per-feature directives |
| `qyl.loom.setup_ai_monitoring` | AI monitoring with the four hard rules |

---

## Routing Hard Rules

- **Always route first.** Never pick a sub-skill from a keyword in the user's message — call `loom_route` (or follow the table above verbatim) and act on its decision.
- **Structured signals win.** If you already have `pullRequestNumber + reviewBotAuthor`, `issueId`, or `repoRoot`, pass them — the router skips keyword matching.
- **Two disjoint matches → clarify.** The router returns `Clarify` with one question when the request spans multiple workflows. Ask the user verbatim; do not guess.
- **SDK setup + AI monitoring together → AI monitoring is the entry point.** AI monitoring requires the base SDK, so the router sequences them automatically.