---
name: qyl-workflow
description: Core workflow for investigating observability data with qyl. Use when working with traces, errors, logs, metrics, or AI agent telemetry from qyl.
license: Apache-2.0
category: workflow
---

# qyl Workflow

Root skill for qyl observability investigations.

## When to Use

- User mentions qyl, traces, spans, logs, errors, or observability
- User wants to debug production issues using telemetry
- User wants to analyze AI agent behavior, token usage, or GenAI metrics
- User wants to set up monitoring for their application

## Available Sub-Skills

| Skill | Use When |
|-------|----------|
| [qyl-fix-issues](../qyl-fix-issues/SKILL.md) | Debugging production errors, investigating exceptions |
| [qyl-code-review](../qyl-code-review/SKILL.md) | Reviewing PRs with observability context |
| [qyl-setup-monitoring](../qyl-setup-monitoring/SKILL.md) | Setting up OTel instrumentation for AI agents |

## qyl Architecture

qyl is an OTLP-native observability platform:

- **Collector** — Ingests traces/logs/metrics via OTLP (gRPC + HTTP), stores in DuckDB
- **Loom** — AI investigation engine (triage, autofix, regression detection, code review)
- **MCP Server** — 93 tools for live telemetry access from AI coding agents
- **Dashboard** — React UI for trace exploration, GenAI monitoring, cost tracking

## Key Concepts

- **OTLP Endpoint**: `http://localhost:4318` (HTTP) or `http://localhost:4317` (gRPC)
- **Traces**: Distributed traces with spans, following OTel semantic conventions
- **GenAI Spans**: AI model calls instrumented with `gen_ai.*` attributes (semconv 1.40)
- **Issues**: Grouped errors with fingerprinting, severity, and lifecycle management
- **Triage**: AI-powered root cause analysis and fix recommendations
- **Autofix**: Automated fix generation with approval gates
