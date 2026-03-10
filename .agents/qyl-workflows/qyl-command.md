---
name: qyl
description: Ask natural-language questions about qyl data and get concise, actionable results using qyl MCP tools
---

# /qyl — qyl Query Command

Use this when a user asks for an observational or investigative query.

## Usage

```bash
/qyl <your natural language query>
```

## Examples

```text
/qyl Show unresolved high priority issues in the last 2 hours
/qyl What is the triage score for issue ISS-1234?
/qyl Which error issues are linked to recent deploys?
/qyl List fix runs for issue ISS-1234
/qyl Is PR #321 in owner/repo still awaiting code-review findings?
```

## How to execute a query

1. Parse intent (issues, triage, fixes, regressions, PR review, handoff state).
2. Call the relevant MCP tool(s):
   - Issue discovery: `qyl.list_error_issues`
   - Issue details: `qyl.get_error_issue`, `qyl.get_error_timeline`
   - Triage: `qyl.get_triage`, `qyl.list_triage`, `qyl.trigger_triage`
   - Fix runs: `qyl.list_fix_runs`, `qyl.get_fix_run`, `qyl.get_fix_run_steps`
   - Regressions: `qyl.list_regressions`, `qyl.get_issue_regressions`
   - Handoffs: `qyl.get_pending_handoffs`, `qyl.get_handoff_context`
   - PR review: `qyl.trigger_code_review`, `qyl.get_code_review`
   - GitHub automation: `qyl.list_github_events`
3. Aggregate results and return an actionable summary.
4. If data is missing, return exactly what is missing and a fallback next query.

## Output format

Use the smallest useful format.

### Issue lists

| Issue ID | Title | Status | Priority | Level | Occurrences | Last Seen | Link |
|----------|-------|--------|----------|-------|-------------|-----------|------|
| ISS-1234 | Timeout in checkout | unresolved | high | error | 2,310 | 4 mins ago | [Open](https://...) |

### Triage view

- **Issue:** ISS-1234
- **Score:** 0.83
- **Automation:** auto / assisted / manual
- **Root Cause:** short hypothesis
- **Suggested path:** trigger fix run / manual inspection / regression check

### PR review view

- **Repo:** owner/repo
- **PR:** 321
- **Status:** reviewed / no issues / not reviewed
- **Issues found:** n
- **Top findings:** file:line, severity, comment

## Response rules

- Always include links when available.
- Use severity labels (`critical`, `high`, `medium`, `low`) in uppercase.
- Keep timestamps readable (`2 mins ago`, `1 hour ago`).
- If query returns no items, include two alternatives.
- Never output secrets, raw request bodies, or full stack traces without redacting sensitive tokens.

## Error handling

If qyl tools return transport or auth errors:

```text
Unable to execute qyl query

Possible causes:
- MCP bridge unavailable
- Collector not running on expected port
- MCP token/auth not configured
- Network or permission issue

Next steps:
1. Check qyl collector/MCP process health
2. Verify collector reachable (default HTTP: 5100)
3. Retry once auth/network is fixed
```
