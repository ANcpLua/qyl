# Dashboard Specification

React 19 frontend for qyl. Operator-grade telemetry UI with dense information surfaces.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Shell](#2-shell)
3. [Telemetry Surfaces](#3-telemetry-surfaces)
4. [Charts](#4-charts)
5. [Copilot](#5-copilot)
6. [Primitives](#6-primitives)
7. [Definition of Done](#7-definition-of-done)

---

## 1. Overview

`src/qyl.dashboard/` — React 19 + Vite 7 + Tailwind CSS 4.

Design philosophy: operator-grade density. Optimize for workflow speed, scanability, and long sessions. Not a marketing site. Not CRUD. This is a telemetry investigation tool.

## 2. Shell

### 2.1 Layout

`Sidebar` — primary navigation. `TopBar` — context controls (project selector, time range, search).

### 2.2 Pages

- `DashboardPage` — overview KPIs and health
- `TracesPage` — trace search and exploration
- `SpanExplorerPage` — span-level detail
- `LogsPage` — structured log explorer
- `GenAIPage` — GenAI-specific telemetry
- `AgentsPage` + `AgentRunDetailPage` — agent execution monitoring
- `IssuesPage` + `IssueTriagePage` + `IssueFixRunsPage` — error tracking and triage
- `ErrorsOutagesPage` — error and outage overview
- `AlertsPage` — alert configuration and history
- `PerformancePage` — performance monitoring
- `SearchPage` — full-text search across telemetry
- `CodeReviewPage` — AI code review results
- `BotPage` + `BotConversationDetailPage` + `BotUserJourneyPage` — bot/agent conversation tracking
- `LoomDashboardPage` — Loom investigation overview
- `WorkflowRunsPage` — workflow execution history
- `SettingsPage` — configuration
- `OnboardingPage` — first-run setup

## 3. Telemetry Surfaces

### 3.1 Tables

Use TanStack Table for all dense data surfaces. Required capabilities per surface:

- Sorting, filtering, pagination
- Column visibility toggles
- Row selection
- Sticky headers
- Keyboard navigation
- Virtualization for large datasets (TanStack Virtual)

Prefer scanable rows, compact spacing, strong typography hierarchy. No cardified rows for log-like data.

### 3.2 Components

- `AgentTraceTree` — hierarchical trace visualization for agent runs
- `CodingAgentResultCard` — coding agent execution results
- `ToolDefinitionsViewer` — GenAI tool call inspection
- `PipelineStatus` (Loom) — autofix pipeline progress
- `LoomSidebar` — Loom investigation navigation
- `CopilotPanel` — AG-UI copilot interface
- `FilterPillBar` — active filter display

## 4. Charts

### 4.1 ECharts

Use for heavy observability surfaces:

- Dense time-series exploration
- Multi-series telemetry charts
- High-point-count screens
- Performance-critical chart rendering

### 4.2 Recharts

Allow for lighter surfaces:

- KPI cards
- Overview dashboards
- Simple bar/line charts
- Admin analytics

If the chart is part of an analysis workflow, use ECharts. If it's decorative, Recharts is fine.

## 5. Copilot

`CopilotPanel` — AG-UI protocol integration via CopilotKit.

`use-copilot.ts` — React hook for copilot state management.
`use-telemetry.ts` — telemetry data hooks.
`use-llm-config.ts` — LLM configuration hooks.

AG-UI protocol: SSE streaming from `CopilotAguiEndpoints` in collector.

## 6. Primitives

### 6.1 Base UI Contract

Single primitive family: `@base-ui/react`. Strict enforcement.

- Never import `@radix-ui/*` or `radix-ui`
- Never use `asChild` or `Slot`
- Composition via `render` prop
- Detached triggers via `createHandle()`
- Forms via Base UI `Form` and `Field` patterns

### 6.2 shadcn/ui

Source-owned design-system starter. Owns:

- App shell, sidebar, command palette
- Dialogs, drawers, settings pages
- Forms, tables, reusable dashboard surfaces

shadcn is the shell layer. Base UI is the primitive layer. They do not compete.

### 6.3 Styling

- Tailwind CSS 4 + CSS variables for design tokens
- Dark mode first-class
- Semantic tokens for telemetry states (severity, health, incident status)
- Dense but readable layouts
- No decorative gradients in operator workflows

## 7. Definition of Done

- [ ] Zero imports from `@radix-ui/*`
- [ ] All dense data surfaces use TanStack Table with sort/filter/paginate
- [ ] Heavy telemetry charts use ECharts
- [ ] AG-UI copilot streams events correctly
- [ ] Dark mode works across all pages
- [ ] Keyboard navigation works for all interactive elements
- [ ] Virtualization applied to surfaces with > 1000 rows
- [ ] No motion that harms scanability or reading speed
- [ ] AI-generated analysis visually distinct from raw telemetry facts
