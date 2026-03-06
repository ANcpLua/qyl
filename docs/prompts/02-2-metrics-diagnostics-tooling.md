# Metrics Extraction — Part 2: Diagnostics Tooling

**Type:** Prompt Chain (2/4)
**Chain:** Metrics API Extraction → `docs/roadmap/loom-design.md` section 15.11
**Source:** `docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md`
**Dependencies:** Run after 02-1

## Prompt

```
Extract from docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md
ONLY Part 2 (dotnet-counters, dotnet-monitor) and append as section 15.11.2 to
docs/roadmap/loom-design.md

Section: "15.11.2 Diagnostics Tooling"
Scope label: CONTEXT-ONLY

Required content:
- Table: Tools (dotnet-counters, dotnet-monitor, dotnet-trace)
  Columns: Tool | Purpose | Metrics Access Method | qyl Equivalent
- qyl equivalents: qyl.watch (SSE live viewer), qyl.mcp (metrics query),
  qyl.dashboard (visualization)
- EventPipe vs OTLP: how .NET-native diagnostics relates to OpenTelemetry
- Loom correlation: Section 5 Capabilities — performance analysis needs
  metrics export paths

Style: Match existing 15.x subsections. Tables preferred. Clear, unambiguous.
Do NOT copy-paste — distill core concepts only.
```
