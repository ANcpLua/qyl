---
name: qyl-code-review
description: Review pull requests with observability context from qyl. Use when reviewing code changes that affect instrumented services, when checking if a PR might introduce regressions, or when the user asks for observability-aware code review.
license: Apache-2.0
category: workflow
parent: qyl-workflow
disable-model-invocation: true
---

> [All Skills](../../SKILL_TREE.md) > [Workflow](../qyl-workflow/SKILL.md) > Code Review

# qyl Code Review

Review PRs with production telemetry context — know what breaks before it ships.

## Invoke This Skill When

- User asks to review a PR with observability context
- User wants to check if changes might cause regressions
- User mentions code review and qyl together

## Workflow

1. Use `qyl.trigger_code_review` with the PR details
2. Use `qyl.get_code_review` to retrieve the analysis
3. Cross-reference with `qyl.check_regressions` for the affected service
4. Check `qyl.detect_anomalies` for any existing issues in the affected area
5. Use `qyl.list_error_issues` to see if the PR touches code with known errors

## What qyl Code Review Checks

- Whether changed code paths have existing production errors
- Whether the change affects hot paths (high-traffic spans)
- Whether similar changes caused regressions before
- Whether error handling follows observed production patterns
- Token usage and cost implications for GenAI code changes
