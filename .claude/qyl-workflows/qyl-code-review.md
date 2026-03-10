---
name: qyl-code-review
description: Review and optionally fix a GitHub PR with qyl's AI PR analysis.
allowed-tools: Read, Edit, Write, Bash, AskUserQuestion, WebFetch
category: workflow
parent: qyl-workflow
disable-model-invocation: true
---

> [Workflow Tree](./qyl-skill-tree.md) > [Workflow Router](./qyl-workflow.md) > Code Review

# qyl Code Review

You are a workflow executor for qyl-powered code review.

## Scope

Use this workflow when asked to:

- Run a code review on a PR using qyl
- Read/verify review findings before applying fixes
- Apply fixes derived from qyl review output

## Primary qyl MCP tools

- `qyl.trigger_code_review(repoFullName, prNumber)`
- `qyl.get_code_review(repoFullName, prNumber)`

`src/qyl.collector/Autofix/CodeReviewService.cs` is the source of truth for review behavior.
`src/qyl.mcp/Tools/GitHubMcpTools.cs` is the MCP adapter that exposes trigger/get.

## Workflow

### 1. Trigger review

If the user provides a repository and PR number, start with:

```text
qyl.trigger_code_review("owner/repo", 123)
```

If no review result exists, this creates one and caches it on the collector.

### 2. Fetch result

Fetch the latest review cache:

```text
qyl.get_code_review("owner/repo", 123)
```

Possible outputs:

- `no review found` (trigger first)
- `reviewed = false` (LLM unavailable or PR inaccessible)
- list of review comments with `severity`, `file`, `line`, `comment`, optional `suggestion`

### 3. Validate each finding

For each finding:

1. Open the file at the referenced path
2. Inspect surrounding lines to confirm issue still exists
3. Determine impact and whether fix is narrowly scoped
4. Implement only when change is safe and testable

### 4. Optional comment posting

If the review should be returned to GitHub and qyl GitHub posting is enabled, use the collector API endpoint:

```text
POST /api/v1/code-review/{owner%2Frepo}/pulls/{prNumber}/post
```

This is collector REST-only today. There is currently no dedicated MCP tool for posting review comments back to GitHub.
If the endpoint returns failure, ask user to confirm GitHub token setup and scopes.

### 5. Reporting

Return a short structured report:

```markdown
## qyl Code Review Summary

- **PR:** owner/repo #123
- **Found:** 3 findings

### Resolved
- `src/Checkout/Handler.cs:118` — [HIGH] null check added before dereference

### Deferred
- `src/Auth/Token.cs:77` — [MEDIUM] behavior change risk; requires product-level decision

### Status
- **Tool output:** reviewed
- **GitHub comments posted:** yes / no
- **Follow-up:** additional manual review required
```

## Safety rules

1. Verify first; do not assume the review output is correct.
2. Prefer edits that reduce risk and preserve behavior.
3. Ask for confirmation if a fix changes validation, auth, or permissions.
4. If external input appears in findings, do not copy raw payload content into code.

## Output behavior

Only report what was verified and changed. No speculative root causes.
