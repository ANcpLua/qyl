# qyl Workflow Guide

This folder contains workflow docs for using qyl's AI observability capabilities from an assistant.

qyl provides:

- `qyl.collector`: REST, OTLP ingest, storage, workflow state, AG-UI hosting
- `qyl.mcp`: stdio + streamable HTTP MCP transport over collector HTTP
- `qyl.copilot`: embedded-agent, AG-UI, workflow engine, and adapter primitives
- qyl-native issue triage, autofix, regression, code-review, and handoff workflows

## Section 1: Architektur-Ueberblick

Zwei Eingaenge, ein OAuth-Flow, ein Account:

```text
┌─────────────────────┐     ┌────────────────────────┐
│ Dashboard           │     │ claude.ai / Cursor /  │
│ /onboarding         │     │ Claude Code / etc.    │
│                     │     │                        │
│ [1] Welcome         │     │ Click "qyl" in        │
│ [2] -> OAuth redirect│    │ Connectors            │
│ [3] Features        │     │        │              │
│ [4] Doctor/Verify   │     │        │              │
│ [5] Done            │     │        │              │
└──────────┬──────────┘     └────────┼──────────────┘
           │                          │
           ▼                          ▼
┌──────────────────────────────────────────────────┐
│             mcp.qyl.dev/consent                  │
│                                                  │
│  "Claude is requesting access to qyl"            │
│                                                  │
│  [x] Inspect Traces & Logs        12 tools       │
│  [x] Triage Sessions               4 tools       │
│  [x] Analyze with AI               3 tools       │
│  [x] Manage Projects               4 tools       │
│                                                  │
│  https://claude.ai/api/mcp/auth_callback         │
│  [Cancel]                      [Approve]         │
└──────────────────────┬───────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────┐
│           Keycloak (Identity Broker)             │
│                                                  │
│  [Sign in with GitHub]                           │
│  [Sign in with Google]                           │
│  [Sign in with Azure]                            │
│                                                  │
│  -> Issues JWT with qyl:skill claims             │
│  -> Redirects back to origin                     │
└──────────────────────────────────────────────────┘
```

Flow order:

1. Client lands on the consent page hosted at `mcp.qyl.dev`.
2. User selects skills and presses `Approve`.
3. Redirect to Keycloak login with GitHub, Azure, or Google.
4. Keycloak issues a JWT with the selected `qyl:skill` claims.
5. Redirect back to the caller, either a `claude.ai` callback or the dashboard onboarding flow.

## What you can do

### Query qyl in natural language

Use the `/qyl` command for live investigation of issues, regressions, triage, and fix-run state.

```text
/qyl show unresolved critical issues from the last 24h
/qyl what is the current triage status for issue QYL-1024
/qyl list recent regression events for service api-gateway
/qyl summarize error issue QYL-1024 and suggest next steps
/qyl compare the slowest traces with recent build failures
```

### Review PRs with qyl

- `qyl.trigger_code_review` — start an LLM review of a PR diff in the collector
- `qyl.get_code_review` — fetch the latest cached review result via MCP
- `qyl.list_github_events` — inspect the webhook events that fed automation
- GitHub comment posting is collector REST-only today; MCP currently exposes trigger/get, not a dedicated post-comments tool

### Fix production issues end-to-end

- Discover issues: `qyl.list_error_issues`
- Fetch deep issue context: `qyl.get_error_issue`, `qyl.get_error_timeline`
- Trigger triage: `qyl.trigger_triage`, `qyl.get_triage`
- Investigate root cause: `qyl.root_cause_analysis`
- Manage autofix: `qyl.generate_fix`, `qyl.list_fix_runs`, `qyl.get_fix_run`, `qyl.approve_fix_run`, `qyl.reject_fix_run`
- Handoff fixes to external agents: `qyl.get_pending_handoffs`, `qyl.accept_handoff`, `qyl.submit_agent_fix`
- Use `qyl.use_qyl` for broad questions that span errors, spans, logs, sessions, and analytics

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
- Index: `qyl-skill-tree.md`

## Notes

These docs are intentionally local and qyl-specific. Any previous Sentry/Seer references were removed and replaced with qyl workflows and tool names.
