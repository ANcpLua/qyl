---
name: Analyze Errors
description: Investigate error patterns from qyl telemetry data
tools: ['telemetry']
trigger: manual
---

# Error Analysis

Analyze recent errors captured by qyl and identify patterns.

## Instructions

1. Query spans with `status_code = 2` (ERROR) from the last {{timeRange}} (default: 1h)
2. Group errors by:
   - `gen_ai.provider.name` (which AI provider)
   - `status_message` (error text)
   - `service_name` (which service)
3. For each error group, report:
   - Occurrence count
   - First and last seen timestamps
   - Sample trace IDs for investigation
4. Identify patterns:
   - Are errors concentrated in one provider or service?
   - Is there a time correlation (burst vs. steady)?
   - Are there common status messages suggesting a root cause?
5. Suggest remediation steps based on error patterns

## Parameters

- **timeRange**: Time window to analyze (default: `1h`, supports: `15m`, `1h`, `6h`, `24h`)
- **severity**: Minimum severity to include (default: `error`)

## Context

{{additionalContext}}
