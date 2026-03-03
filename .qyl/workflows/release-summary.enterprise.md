---
name: release-summary.enterprise
description: Enterprise release assessment with risk controls, approvals, and audit references.
tools:
  - search_spans
  - search_logs
  - get_genai_stats
  - get_system_context
trigger: manual
---

# Objective

Produce an executive and compliance-ready release report.

# Process

1. Summarize release health metrics and risk posture.
2. Identify policy-sensitive changes and approval checkpoints.
3. Capture trace/log evidence for all critical claims.
4. Recommend go/no-go with explicit rationale.

# Output Format

## Executive Decision
- Recommended status: go / conditional / no-go
- Rationale:

## Risk and Controls
- Operational risk:
- Security/privacy risk:
- Required approvals:

## Audit References
- Trace references:
- Log references:
- Time window:

## Action Register
- Action:
- Owner:
- Due date:
- Approval state:
