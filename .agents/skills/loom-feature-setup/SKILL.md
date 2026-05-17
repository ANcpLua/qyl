---
name: loom-feature-setup
description: Configure specific qyl features — OpenTelemetry exporter pipelines that ship telemetry into the qyl collector, and qyl alert rules. Routes to the correct feature-config skill; asks one clarifying question when signals conflict.
license: Apache-2.0
role: router
---

# Loom Feature Setup

qyl is OTLP-native: applications instrument with OpenTelemetry and the OTel Collector ships traces/metrics/logs to the qyl collector. There is no qyl vendor SDK. This page routes to the two feature-config skills that exist on top of that pipeline.

## How to Fetch Skills

Skills live under `.claude/skills/<skill-name>/SKILL.md` in this repo. Read them directly; do not summarise via tools that truncate.

## Start Here — Read This Before Doing Anything

**Do not skip this section.** Do not assume which feature the user needs. Ask first.

1. If the user mentions **OpenTelemetry, OTel Collector, OTLP export, gen_ai spans, LLM tracing, or routing telemetry into qyl** → `qyl-otel-exporter-setup`
2. If the user mentions **alerts, alert rules, notifications, error-rate alerts, threshold alerts, burn-rate alerts, anomaly alerts, regression alerts** → `loom-create-alert`

When unclear, **ask the user** which feature they want to configure. Do not guess.

---

## Feature Skills

| Use when | Skill | Path |
|---|---|---|
| OpenTelemetry Collector pipeline exporting OTLP into the qyl collector — multi-service routing, resource attributes, gen_ai spans | [`qyl-otel-exporter-setup`](../qyl-otel-exporter-setup/SKILL.md) | `qyl-otel-exporter-setup/SKILL.md` |
| Creating alert rules via qyl's Alerts API — threshold, error-rate, new-issue, regression, burn-rate, anomaly, custom | [`loom-create-alert`](../loom-create-alert/SKILL.md) | `loom-create-alert/SKILL.md` |

Each skill carries its own detection logic, prerequisites, and step-by-step instructions. Trust the skill — read it carefully and follow it. Do not improvise or take shortcuts.

---

## Routing Hard Rules

- **Always route first.** Never pick a feature skill from a keyword in the user's message — match the bullet list in *Start Here* verbatim or ask one clarifying question.
- **OTel exporter is the prerequisite.** Alerts only fire against telemetry that has actually reached the qyl collector. If the user asks to create an alert on a metric or span that has never been emitted, surface that gap before building the rule — the alert will never fire against an empty table.

---

## Parent Router

This skill is a child of the top-level `loom` skill (`.claude/skills/SKILL.md`). If the user hasn't chosen a feature vs. a workflow yet, return to the top-level router rather than guessing.
