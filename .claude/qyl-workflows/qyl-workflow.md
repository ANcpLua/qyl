---
name: qyl-workflow
description: Route user requests to the right qyl workflow across qyl.collector, qyl.mcp, and qyl.copilot.
license: Apache-2.0
role: router
---

> [Workflow Tree](./qyl-skill-tree.md)

# qyl Workflow Router

Use this document when deciding which workflow file to invoke.

## Ask first when unclear

If request intent is ambiguous, ask one clarifying question before taking action.

## Routing rules

1. If user asks to investigate/resolve runtime issues, trigger triage, reduce error rates, or create a fix run → `qyl-fix-issues`
2. If user asks to review a specific PR with qyl → `qyl-code-review`
3. If user asks to review PRs in bulk, or asks if qyl found PR findings → `qyl-pr-code-review`
4. If user asks for data summary, cross-domain observability questions, or natural-language investigation queries → `qyl-command` (`/qyl`, often backed by `qyl.use_qyl`)
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
- Treat `src/qyl.collector` as the domain source of truth, `src/qyl.mcp` as the MCP adapter, and `src/qyl.copilot` as the embedded-agent/workflow layer.
