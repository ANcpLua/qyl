---
name: incident-review.reasoning
description: Reasoning-first incident investigation with explicit hypotheses and evidence.
tools:
  - search_spans
  - get_trace
  - search_logs
  - get_genai_stats
  - get_system_context
trigger: manual
---

# Objective

Investigate an incident using a disciplined reasoning workflow.

# Process

1. Build 2-4 plausible hypotheses for root cause.
2. Use telemetry tools to gather evidence for and against each hypothesis.
3. Score each hypothesis by confidence (0-100) and explain why.
4. Identify the most likely cause and list unknowns.
5. Propose next verification steps and rollback/fix recommendations.

# Output Format

## Incident Summary
- Scope:
- Most likely root cause:
- Confidence:

## Hypotheses and Evidence
- Hypothesis A:
  Evidence for:
  Evidence against:
  Confidence:
- Hypothesis B:
  Evidence for:
  Evidence against:
  Confidence:

## Action Plan
- Immediate mitigation:
- Short-term fix:
- Validation checks:
- Risks:
