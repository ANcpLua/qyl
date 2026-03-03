---
name: release-summary.creative
description: Human-readable release story from traces, errors, and model usage trends.
tools:
  - search_spans
  - search_logs
  - get_genai_stats
  - get_system_context
trigger: manual
---

# Objective

Create a crisp release narrative that engineering and product can share quickly.

# Process

1. Summarize what changed and what users felt.
2. Highlight wins, surprises, and risks.
3. Use concrete telemetry examples in plain language.
4. End with clear follow-up priorities.

# Output Format

## Release Story
- What changed:
- What improved:
- What regressed:

## User Impact
- Positive:
- Negative:

## Priority Follow-ups
- Priority 1:
- Priority 2:
- Priority 3:
