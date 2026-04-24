---
name: loom-workflow
description: Route a user request across the five Loom workflow shapes (fix production issue, review sentry[bot]/seer-by-sentry[bot] PR comments, set up Sentry .NET SDK, set up AI monitoring, run headless autofix pipeline). Use when the user mentions Sentry, seer, production errors, PR bot feedback, setting up error/tracing/profiling/logging/metrics/crons/AI-monitoring in a .NET project, or asking Loom to auto-generate a fix diff. Never guess across workflows — this skill + the loom_route MCP tool force a clarifying question when signals conflict.
---

# loom-workflow — Router across Loom's five workflow shapes

Loom (`services/qyl.loom`) exposes a deterministic workflow router over MCP that dispatches user requests to one of five specialised workflow skills. This skill is the entry point.

## Invoke this skill when
- The user mentions fixing Sentry / qyl errors, debugging production bugs, or investigating exceptions.
- The user mentions `qyl[bot]`, `qyl-review[bot]`, or "review PR comments".
- The user asks to install / configure Sentry in a .NET project (error monitoring, tracing, profiling, logging, metrics, crons).
- The user asks to monitor LLM / OpenAI / Anthropic / `gen_ai` calls.
- The user asks Loom to auto-generate a fix diff / run the headless autofix pipeline on an issue.
- You are unsure which of the five workflows applies — **always route first, do not guess**.

## The five workflows this router dispatches to

| Workflow | Next skill | When |
|---|---|---|
| Fix a production issue | `loom-fix-issues` | "fix production bug", "investigate Sentry exception", "resolve error ABC-123" |
| Review bot PR comments | `loom-review-bot-pr` | "resolve qyl[bot] comments on PR #42", "qyl review feedback" |
| Set up .NET SDK | `loom-sdk-onboarding` | "add Sentry to this .NET app", "install Sentry.AspNetCore" |
| Set up AI monitoring | `loom-ai-monitoring` | "monitor LLM calls", "gen_ai spans", "track OpenAI usage" |
| Headless autofix pipeline | `loom-autofix` | "auto-fix this issue", "run Loom on ABC-123", "generate a diff for this error" |

## How to run this skill

### Step 1 — Route via the MCP tool, not by guessing

The qyl.loom MCP server exposes `loom_route`. Call it with the user's natural-language request plus any structured signals you already have (PR number, bot author login, issue id). The tool returns:

```json
{
  "kind": "FixProductionIssue | ReviewBotPrComments | SetupDotnetSdk | SetupAiMonitoring | Autofix | Clarify",
  "confidence": 1.0,
  "rationale": "...",
  "promptIds": ["qyl.loom.<picked>"],
  "matchedSignals": ["..."],
  "clarifyingQuestion": null
}
```

### Step 2 — Act on the decision

- `Clarify` → ask the user the `clarifyingQuestion` verbatim, wait for the answer, then call `loom_route` again. Do **not** guess.
- Any other kind → hand off to the named skill (`loom-fix-issues`, `loom-review-bot-pr`, `loom-sdk-onboarding`, `loom-ai-monitoring`, `loom-autofix`) AND fetch the MCP prompt(s) listed in `promptIds`. The specialised skill will then drive the workflow.

### Step 3 — Chain detection / parsing explicitly

Once `loom_route` returns a workflow, call the workflow-specific tool yourself: `loom_detect_dotnet(repoRoot)` for onboarding / AI-monitoring, or `loom_parse_review_bot_comments(commentsJson, additionalBotLoginsJson?)` for the PR-review workflow. One tool per step keeps the decision trail visible in the MCP call log — a single meta-tool that fanned out to both was removed to avoid schema drift across three tool descriptors.

## Hard rules

- **Structured signals win.** If the caller already has `(pullRequestNumber + reviewBotAuthor)` or `issueId`, pass them to `loom_route` — the tool skips keyword matching and returns a deterministic decision.
- **Two disjoint matches → clarify.** The router returns `Clarify` when the request overlaps two unrelated workflows. Treat that as a hard stop, not a suggestion.
- **SDK + AI monitoring is not ambiguous.** When both trigger, the router picks `SetupAiMonitoring` and flags `SetupDotnetSdk` as a prerequisite. Follow that order.
- **Never skip the router.** Even if the request looks obvious, call `loom_route` first so the decision is logged and consistent with the prompt the specialised skill will fetch.

## MCP surface this skill uses

| Tool | Purpose |
|---|---|
| `loom_route` | Route a request deterministically; returns `Clarify` when ambiguous. |

| Prompt | Purpose |
|---|---|
| `qyl.loom.route` | LLM-side router prompt — mirrors `loom_route` for agents that want chat-style routing. |

## Troubleshooting

| Issue | Fix |
|---|---|
| `loom_route` keeps returning `Clarify` | The request literally is ambiguous. Ask the user the supplied question; do not try harder. |
| Two workflows feel right | The router has already picked — if you disagree, re-invoke with stronger signals (issue id, PR number, repo path). |
| No MCP server available | `qyl.loom` must be running on the user's machine (default `mcp/loom`). Without it, fall back to calling the pure primitives described in `services/qyl.loom/Workflows/` directly — they are static, no DI required. |