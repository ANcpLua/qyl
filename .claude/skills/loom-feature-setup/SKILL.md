---
name: loom-feature-setup
description: Configure specific qyl features beyond basic SDK setup. Use when asked to monitor AI/LLM calls, set up OpenTelemetry pipelines that export to the qyl collector, or create qyl alert rules. Routes to the correct feature-config skill; asks one clarifying question when signals conflict.
license: Apache-2.0
role: router
---

# Loom Feature Setup

Configure specific qyl capabilities beyond basic SDK setup — AI monitoring, OpenTelemetry exporter pipelines, and alerts. This page routes to the right feature-config skill.

## How to Fetch Skills

Skills live under `.claude/skills/<skill-name>/SKILL.md` in this repo. Read them directly; do not summarise via tools that truncate.

## Start Here — Read This Before Doing Anything

**Do not skip this section.** Do not assume which feature the user needs. Ask first.

1. If the user mentions **AI monitoring, LLM tracing, `gen_ai.*` spans, token usage, or instrumenting OpenAI / Anthropic / Microsoft.Extensions.AI** → `loom-ai-monitoring`
2. If the user mentions **OpenTelemetry, OTel Collector, OTLP export, or multi-service telemetry routing into qyl** → `qyl-otel-exporter-setup`
3. If the user mentions **alerts, alert rules, notifications, error-rate alerts, threshold alerts, burn-rate alerts, anomaly alerts, regression alerts** → `loom-create-alert`
4. If the user mentions **installing the base SDK (error / tracing / profiling / logging / metrics / crons) rather than a feature on top of it** → defer to `loom-sdk-onboarding` (sibling workflow skill, not a feature-config skill).

When unclear, **ask the user** which feature they want to configure. Do not guess.

---

## Feature Skills

| Use when | Skill | Path |
|---|---|---|
| Wiring AI agent monitoring (`gen_ai.*`) — tracing-first, sampling gate, PII opt-in | [`loom-ai-monitoring`](../loom-ai-monitoring/SKILL.md) | `loom-ai-monitoring/SKILL.md` |
| OpenTelemetry Collector pipeline exporting OTLP into the qyl collector — multi-service routing, resource attributes | [`qyl-otel-exporter-setup`](../qyl-otel-exporter-setup/SKILL.md) | `qyl-otel-exporter-setup/SKILL.md` |
| Creating alert rules via qyl's Alerts API — threshold, error-rate, new-issue, regression, burn-rate, anomaly, custom | [`loom-create-alert`](../loom-create-alert/SKILL.md) | `loom-create-alert/SKILL.md` |
| Detection-first .NET telemetry SDK setup (base layer the feature skills build on) | [`loom-sdk-onboarding`](../loom-sdk-onboarding/SKILL.md) | `loom-sdk-onboarding/SKILL.md` |

Each skill carries its own detection logic, prerequisites, and step-by-step instructions. Trust the skill — read it carefully and follow it. Do not improvise or take shortcuts.

---

## Routing Hard Rules

- **Always route first.** Never pick a feature skill from a keyword in the user's message — match the bullet list in *Start Here* verbatim or ask one clarifying question.
- **SDK setup + AI monitoring together → AI monitoring is the entry point.** AI monitoring requires the base SDK; `loom-ai-monitoring` references `loom-sdk-onboarding` as prerequisite and sequences the two automatically.
- **OTel exporter + AI monitoring together → set up the exporter first.** AI spans need a working OTLP pipeline before they reach the qyl collector; wire the transport before instrumenting the producer.
- **Alert creation presupposes running telemetry.** If the user asks to create an alert on a metric or span that has never been emitted, surface that gap before building the rule — the alert will never fire against an empty table.

---

## Parent Router

This skill is a child of the top-level `loom` skill (`.claude/skills/SKILL.md`). If the user hasn't chosen a feature vs. a workflow yet, return to the top-level router rather than guessing.
