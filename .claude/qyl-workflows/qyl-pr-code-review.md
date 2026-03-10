---
name: qyl-pr-code-review
description: Review and resolve PRs with qyl code-review findings; optionally prepare or apply fixes.
category: workflow
parent: qyl-workflow
license: Apache-2.0
disable-model-invocation: true
---

> [Workflow Tree](./qyl-skill-tree.md) > [Workflow Router](./qyl-workflow.md) > PR Code Review

# qyl PR Review Workflow

Use this when the user explicitly asks to review a PR flow, or to process a PR with unresolved qyl review signals.

## Prerequisites

- PR context available (`owner/repo` + PR number)
- Optional: `gh` installed if user wants PR listing/inspection outside MCP

## Step 1 — identify target PR

- If PR is provided, use it directly.
- If not provided, ask the user for PR number and repo and suggest recent open PRs with
  `qyl.list_github_events` filtered by `eventType: pull_request`.

qyl currently exposes review execution per PR. There is no separate MCP queue or batch-review object to consume.

## Step 2 — run or refresh review

1. `qyl.trigger_code_review(repo, prNumber)`
2. `qyl.get_code_review(repo, prNumber)`

## Step 3 — execute fix loop

For each finding:

- Validate file + line still exist
- Apply fix if it is a deterministic bug
- Skip and flag risky or false-positive findings
- Keep all edits minimal and include tests where reasonable

## Step 4 — summarize

### Resolved

| File:Line | Severity | Fix | Result |
|-----------|----------|-----|--------|
| src/Checkout.cs:142 | HIGH | Added guard for null path | Applied |

### Deferred

| File:Line | Severity | Reason |
|-----------|----------|--------|
| src/Auth.cs:55 | MEDIUM | Behavioral contract change needed | Deferred |

Include totals and next actions (re-run review, manual inspection, etc.).

## When things can’t be fixed immediately

If a finding appears to be stale, irrelevant, or environment-specific:

- Do not alter it blindly
- Mark as deferred
- Ask for repo/branch context, expected behavior, or tests

## Optional handoff boundary

If a fix requires deeper refactoring, suggest moving the issue to handoff/agent workflow:

- `qyl.get_pending_handoffs`
- `qyl.submit_agent_fix`
