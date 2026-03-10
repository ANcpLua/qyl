# qyl Skill Tree

Use this as a local entry point for qyl workflow docs.

## Start here

If intent is unclear, read:

1. `qyl-workflow.md`
2. `qyl-command.md` or one of the workflow files

## Entry Points

- `qyl-workflow.md` — router and dispatch rules
- `qyl-command.md` — `/qyl` natural-language command

## Workflows

| Use when | Skill | Path |
|---|---|---|
| Run AI-assisted PR review for a given PR | `qyl-code-review` | `qyl-code-review.md` |
| Review multiple PR findings and manage PR review loop | `qyl-pr-code-review` | `qyl-pr-code-review.md` |
| Investigate or fix production issues | `qyl-fix-issues` | `qyl-fix-issues.md` |

## Quick lookup by keywords

| Keywords | Skill |
|---|---|
| issue, error, exception, production, incident | `qyl-fix-issues.md` |
| pull request, pr review, review comments | `qyl-code-review.md` |
| review all prs, unresolved review, review loop | `qyl-pr-code-review.md` |
| summarize, query, qyl stats | `qyl-command.md` |

## Maintenance

- Add/update one workflow file at a time.
- Keep references in this file aligned with updated behavior.
