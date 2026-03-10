# qyl Workflow Guide

This folder contains workflow docs for using qyl's AI observability capabilities from an assistant.

qyl provides:

- REST + MCP control surface for error triage and autofix
- LLM-assisted code review for PRs
- GitHub integration for webhook-backed automation
- Regression detection and agent handoff flows
- Dashboard/telemetry query workflows

## What you can do

### Query qyl in natural language

Use the `/qyl` command for live investigation of issues, regressions, triage, and fix-run state.

```text
/qyl show unresolved critical issues from the last 24h
/qyl what is the current triage status for issue QYL-1024
/qyl list recent regression events for service api-gateway
/qyl summarize error issue QYL-1024 and suggest next steps
```

### Review PRs with qyl

- `qyl.trigger_code_review` — start an LLM review of a PR diff
- `qyl.get_code_review` — fetch the latest cached review result
- `qyl.list_github_events` — inspect the raw events that fed automation

### Fix production issues end-to-end

- Discover issues: `qyl.list_error_issues`
- Fetch deep issue context: `qyl.get_error_issue`, `qyl.get_error_timeline`
- Trigger triage: `qyl.trigger_triage`
- Manage autofix: `qyl.list_fix_runs`, `qyl.get_fix_run`, `qyl.approve_fix_run`, `qyl.reject_fix_run`
- Handoff fixes to external agents: `qyl.get_pending_handoffs`, `qyl.accept_handoff`, `qyl.submit_agent_fix`

## Quick start

1. Run `/qyl` for the user’s immediate goal.
2. Use `qyl-workflow.md` to pick the right workflow.
3. Follow the workflow doc step-by-step and report concrete IDs and results.
4. When changes are made, validate with the build/test rules in this repo (`nuke`, `Playwright`, MCP checks where relevant).

## File map

- Router: `qyl-workflow.md`
- Skills/workflows:
  - `qyl-code-review.md`
  - `qyl-pr-code-review.md`
  - `qyl-fix-issues.md`
- Command: `qyl-command.md`
- Index: `SKILL_TREE.md`

## Notes

These docs are intentionally local and qyl-specific. Any previous Sentry/Seer references were removed and replaced with qyl workflows and tool names.
