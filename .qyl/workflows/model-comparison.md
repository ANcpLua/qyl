---
name: Model Comparison
description: Compare GenAI model performance, cost, and quality metrics
tools: ['telemetry']
trigger: manual
---

# Model Comparison

Compare GenAI model performance across providers and models.

## Instructions

1. Query GenAI spans from the last {{timeRange}} (default: 24h)
2. For each unique `gen_ai.request.model`, compute:
   - **Throughput**: Total requests, requests/minute
   - **Latency**: Average, P50, P95, P99 duration
   - **Tokens**: Average input tokens, average output tokens, total tokens
   - **Cost**: Total cost, average cost per request, cost per 1K output tokens
   - **Reliability**: Success rate (% status_code != 2), error count
   - **Speed**: Tokens per second (output_tokens / duration_seconds)
3. Create a comparison table sorted by:
   - Cost efficiency (cost per 1K output tokens, ascending)
   - Speed (tokens/second, descending)
   - Reliability (success rate, descending)
4. Highlight recommendations:
   - Best value: lowest cost per 1K tokens with >95% success rate
   - Fastest: highest tokens/second
   - Most reliable: highest success rate
5. Flag models with concerning metrics:
   - Success rate < 90%
   - P95 latency > 30s
   - Cost per request > $0.10

## Parameters

- **timeRange**: Time window to compare (default: `24h`)
- **providers**: Comma-separated provider filter (default: all)

## Context

{{additionalContext}}
