---
name: release-summary.reasoning
description: Compare release windows and reason about regressions using telemetry evidence.
tools:
  - search_spans
  - search_logs
  - get_genai_stats
  - get_system_context
trigger: manual
---

# Objective

Assess release quality through before/after telemetry reasoning.

# Process

1. Compare error, latency, and token usage before vs after release.
2. Identify statistically meaningful shifts and likely causes.
3. Separate correlation from likely causation in findings.
4. Provide confidence levels and missing evidence.

# Output Format

## Release Delta
- Error rate delta:
- Latency delta:
- Cost/token delta:

## Regression Analysis
- Confirmed regressions:
- Suspected regressions:
- Non-issues:

## Recommendation
- Ship / hold / rollback:
- Confidence:
- Evidence gaps:
