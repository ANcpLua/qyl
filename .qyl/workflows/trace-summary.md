---
name: Trace Summary
description: Summarize trace data for a time range with latency and cost analysis
tools: ['telemetry']
trigger: manual
---

# Trace Summary

Generate a summary of trace data from qyl for the specified time range.

## Instructions

1. Query all spans from the last {{timeRange}} (default: 1h)
2. Compute summary statistics:
   - Total span count
   - Unique trace count
   - Unique session count
   - Average, P50, P95, P99 duration (from `duration_ns`)
3. Break down by `gen_ai.provider.name`:
   - Request count per provider
   - Total input/output tokens per provider
   - Average latency per provider
   - Total estimated cost (`gen_ai_cost_usd`)
4. Break down by `gen_ai.request.model`:
   - Request count per model
   - Token usage per model
   - Cost per model
5. Identify anomalies:
   - Unusually slow requests (> 3x P95)
   - High-cost requests
   - Failed requests (status_code = 2)
6. Present findings in a clear markdown table format

## Parameters

- **timeRange**: Time window to summarize (default: `1h`)
- **groupBy**: Primary grouping dimension (default: `provider`, options: `provider`, `model`, `service`)

## Context

{{additionalContext}}
