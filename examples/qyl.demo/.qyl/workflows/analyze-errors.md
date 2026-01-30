---
name: Analyze Errors
description: Investigate recent error patterns from telemetry
tools: ['telemetry', 'codebase']
trigger: manual
---

# Error Analysis Workflow

Analyze recent errors from qyl telemetry and suggest fixes.

## Instructions

1. Query the most recent errors from the telemetry data
2. Group errors by type and frequency
3. Identify root causes where possible
4. Suggest code fixes or configuration changes

## Parameters

- **timeRange**: Time window to analyze (default: last 1 hour)
- **severity**: Minimum severity level (error, warning, info)

## Context

{{additionalContext}}
