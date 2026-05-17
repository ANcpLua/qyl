---
name: loom-workflow
description: Route a user request across the three Loom workflow shapes (fix production issue, process review-bot PR comments, run headless autofix pipeline). Use when the user mentions qyl, production errors, PR bot feedback, or asking Loom to auto-generate a fix diff. Never guess across workflows — this skill + the loom_route MCP tool force a clarifying question when signals conflict.
---

# loom-workflow — Router across Loom's three workflow shapes

Loom (`services/qyl.loom`) exposes a deterministic workflow router over MCP that dispatches user requests to one of three specialised workflow skills. This skill is the entry point.

qyl is OTLP-native — there is no vendor SDK onboarding workflow. Telemetry pipeline setup lives in the sibling `qyl-otel-exporter-setup` skill, not here.

## Invoke this skill when
- The user mentions fixing qyl / qyl errors, debugging production bugs, or investigating exceptions.
- The user mentions `qyl[bot]`, `qyl-review[bot]`, or "review PR comments".
- The user asks Loom to auto-generate a fix diff / run the headless autofix pipeline on an issue.
- You are unsure which of the three workflows applies — **always route first, do not guess**.

## The three workflows this router dispatches to

| Workflow | Next skill | When |
|---|---|---|
| Fix a production issue | `loom-fix-issues` | "fix production bug", "investigate qyl exception", "resolve error ABC-123" |
| Review bot PR comments | `loom-review-bot-pr` | "resolve qyl[bot] comments on PR #42", "qyl review feedback" |
| Headless autofix pipeline | `loom-autofix` | "auto-fix this issue", "run Loom on ABC-123", "generate a diff for this error" |

## How to run this skill

### Step 1 — Route via the MCP tool, not by guessing

The qyl.loom MCP server exposes `loom_route`. Call it with the user's natural-language request plus any structured signals you already have (PR number, bot author login, issue id). The tool returns:

```json
{
  "kind": "FixProductionIssue | ReviewBotPrComments | Autofix | Clarify",
  "confidence": 1.0,
  "rationale": "...",
  "promptIds": ["qyl.loom.<picked>"],
  "matchedSignals": ["..."],
  "clarifyingQuestion": null
}
```

### Step 2 — Act on the decision

- `Clarify` → ask the user the `clarifyingQuestion` verbatim, wait for the answer, then call `loom_route` again. Do **not** guess.
- Any other kind → hand off to the named skill (`loom-fix-issues`, `loom-review-bot-pr`, `loom-autofix`) AND fetch the MCP prompt(s) listed in `promptIds`. The specialised skill will then drive the workflow.

### Step 3 — Chain the workflow's specific tool

Once `loom_route` returns a workflow, call the workflow-specific tool yourself: `loom_parse_review_bot_comments(commentsJson, additionalBotLoginsJson?)` for the PR-review workflow. One tool per step keeps the decision trail visible in the MCP call log.

## Hard rules

- **Structured signals win.** If the caller already has `(pullRequestNumber + reviewBotAuthor)` or `issueId`, pass them to `loom_route` — the tool skips keyword matching and returns a deterministic decision.
- **Two disjoint matches → clarify.** The router returns `Clarify` when the request overlaps two unrelated workflows. Treat that as a hard stop, not a suggestion.
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
