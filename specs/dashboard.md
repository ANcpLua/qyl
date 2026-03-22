# Dashboard Specification

> Owner: dashboard
> SSOT: YES (React UI, component library, chart strategy, Base UI contract)
> Depends on: `api.md` (response contract), `collector.md` (REST API)
> Used by: none (leaf node)

React 19 frontend for qyl. Operator-grade telemetry UI with dense information surfaces.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Shell](#2-shell)
3. [Telemetry Surfaces](#3-telemetry-surfaces)
4. [Charts](#4-charts)
5. [Realtime and Handoff](#5-realtime-and-handoff)
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
- `GenAIPage` — GenAI-specific telemetry (model, tokens, latency, cost per call)
- `CostPage` — per-model, per-service, per-session cost aggregations and budget alerts
- `IssuesPage` — error tracking and grouping
- `ErrorsOutagesPage` — error and outage overview
- `AlertsPage` — alert configuration and history
- `PerformancePage` — performance monitoring
- `SearchPage` — full-text search across telemetry
- `ServicesPage` — service map, dependencies, health
- `SettingsPage` — pricing table, retention, API keys
- `OnboardingPage` — first-run setup

Deleted per v2 architecture (see `00-architecture.md` section 3.2):

- `CodeReviewPage`, `IssueTriagePage`, `IssueFixRunsPage` → Loom's responsibility
- `BotPage`, `BotConversationDetailPage`, `BotUserJourneyPage` → deleted
- `LoomDashboardPage` → Loom's own UI
- `AgentsPage`, `AgentRunDetailPage` → deleted (Loom monitors its own agents)

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
- `ToolDefinitionsViewer` — GenAI tool call inspection
- `FilterPillBar` — active filter display
- `CostSummaryCard` — cost KPIs (spend today, top model, budget status)

Deleted (Loom-owned): `CodingAgentResultCard`, `PipelineStatus`, `LoomSidebar`, `LoomHandoffPanel`.

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

## 5. Realtime

SSE streams hosted by `qyl.collector` push live telemetry (spans, logs, metrics) to the dashboard.

- `ObservationSubscription` + `SubscriptionManager` handle per-client subscriptions
- `SchemaVersionNegotiator` handles client/server schema compatibility
- Issues are REST-only (not streamed)

The dashboard is an operator surface over telemetry, not a Copilot shell or agent UI.

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

- [x] Zero imports from `@radix-ui/*`
- [ ] All dense data surfaces use TanStack Table with sort/filter/paginate (CostPage only — TracesPage uses @tanstack/react-virtual with raw flex rows, LogsPage uses raw flex rows, IssuesPage uses raw markup, ServicesPage uses manual sort without TanStack Table; all four need migration — not implemented, tracked as future work)
- [x] Heavy telemetry charts use ECharts
- [ ] Realtime handoff streams attach and resume correctly
- [x] Dark mode works across all pages (CSS variable token system in index.css)
- [ ] Keyboard navigation works for all interactive elements (partial — 29 files have aria/onKeyDown but not audited)
- [x] Virtualization applied to surfaces with > 1000 rows (TracesPage, LogsPage use @tanstack/react-virtual)
- [x] No motion that harms scanability, keyboard usage, responsiveness, or reading speed
- [ ] AI-generated analysis visually distinct from raw telemetry facts
- [ ] Accessibility semantics preserved across all components
- [ ] No hidden conventions introduced that a future agent cannot understand
- [ ] Lint and CI checks pass
