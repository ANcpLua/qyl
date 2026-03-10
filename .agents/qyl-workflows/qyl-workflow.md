---
name: qyl-workflow
description: Route user requests to the right qyl workflow: issue remediation, PR review, triage, or regression checks.
license: Apache-2.0
role: router
---

> [All Skills](../../SKILL_TREE.md)

# qyl Workflow Router

Use this document when deciding which workflow file to invoke.

## Ask first when unclear

If request intent is ambiguous, ask one clarifying question before taking action.

## Routing rules

1. If user asks to investigate/resolve runtime issues, trigger triage, or reduce error rates → `qyl-fix-issues`
2. If user asks to review a specific PR with qyl → `qyl-code-review`
3. If user asks to review PRs in bulk, or asks if qyl found PR findings → `qyl-pr-code-review`
4. If user asks for data summary or natural-language investigation queries → `qyl-command` (`/qyl`)
5. If user asks about regression alerts or repeated incidents after deploy → `qyl-fix-issues` (regression phase)

## Workflow map

| Intent | Skill file | Purpose |
|---|---|---|
| Investigate issue / bug reports | `qyl-fix-issues.md` | Issue context, triage, fix run decisions |
| Review a specific PR | `qyl-code-review.md` | Run qyl PR analysis and patch findings |
| Review PRs for workflow findings | `qyl-pr-code-review.md` | Handle review loops / deferred findings |
| Ask natural-language operational queries | `qyl-command.md` | `/qyl` command dispatcher |

## Rules

- Follow the selected workflow’s constraints before taking action.
- When possible, return IDs and exact tool calls in the final report.
- Do not assume PR review or fix status without checking tool output.
