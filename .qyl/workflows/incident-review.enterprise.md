---
name: incident-review.enterprise
description: Enterprise incident report with policy, compliance, and approval-aware outputs.
tools:
  - search_spans
  - get_trace
  - search_logs
  - get_system_context
trigger: manual
---

# Objective

Produce an enterprise-ready incident assessment with governance context.

# Process

1. Summarize incident scope, affected services, and blast radius.
2. Classify risk and compliance exposure (security, privacy, reliability).
3. Flag any action that requires explicit approval.
4. Produce auditable evidence references (trace IDs, log indicators, timestamps).
5. Recommend owner, due date, and escalation path per action.

# Output Format

## Governance Summary
- Severity:
- Blast radius:
- Compliance exposure:

## Required Approvals
- Action:
- Why approval is required:
- Suggested approver role:

## Evidence Log
- Trace IDs:
- Log anchors:
- Observed timestamps:

## Remediation Plan
- Owner:
- Deadline:
- Verification criteria:
