You are reviewing or modifying the qyl dashboard (the primary telemetry visualizer).

### SCOPE

Includes:

- src/qyl.dashboard/src/pages/*.tsx (GenAIPage, LogsPage, MetricsPage, ResourcesPage, SettingsPage, TracesPage)
- src/qyl.dashboard/src/hooks/use-telemetry.ts (SSE streaming client)
- src/qyl.dashboard/src/lib/utils.ts (cn() helper)
- src/qyl.dashboard/src/types/ (generated from core/generated/typescript via SyncGeneratedTypes)
- src/qyl.dashboard/src/components/layout/ (DashboardLayout, Sidebar, TopBar)
- src/qyl.dashboard/src/components/ui/ (shadcn/ui components)

### GOAL

Provide a fast, schema-correct, streaming-capable UI that renders telemetry without
transforming or reinterpreting the data model.

### REQUIRED ACTIONS

1. Schema Fidelity
  - Dashboard types MUST originate from core/generated/typescript (Kiota generated).
  - Use `nuke SyncGeneratedTypes` to copy to src/qyl.dashboard/src/types/generated/.
  - Pages MUST NOT redefine schemas locally.
  - MUST NOT rename fields — server sends snake_case; dashboard consumes as-is.

2. REST Client Rules (api.ts)
  - MUST call collector/api endpoints exactly as specified.
  - MUST use strongly-typed responses based on generated TS types.
  - MUST NOT reshape objects before passing them to components.

3. Streaming (SSE) Rules
  - useTelemetry.ts MUST rely on:
    EventSource / fetch-SSE / Typed SSE wrapper
  - MUST support auto-reconnect.
  - MUST parse JSON payloads according to DTO structure.
  - MUST NOT run unnecessary JSON.parse conversions.

4. Page Rendering Rules
  - Components MUST be pure & deterministic given telemetry state.
  - MUST handle empty states, missing fields, and partial session data.
  - MUST compute aggregates client-side only when trivial (e.g., UI grouping).
  - Heavy analytics MUST remain in the collector.

5. UI Consistency
  - Tailwind v4 MUST be used for layout; no mixed inline styles.
  - React 19 best practices MUST be used (useTransition, useMemo, etc.).
  - MUST avoid rendering loops and unnecessary re-renders.

6. Dependency Rules
  - src/qyl.dashboard/* MAY depend on:
    core/generated/typescript (via SyncGeneratedTypes)
    src/qyl.dashboard/src/hooks/
    src/qyl.dashboard/src/lib/
    src/qyl.dashboard/src/types/
  - src/qyl.dashboard/* MUST NOT depend on:
    src/qyl.collector/*
    instrumentation/*
    eng/qyl.cli/*
    storage/*
    processing/*

7. Error Handling
  - MUST surface SSE disconnects, REST failures, schema mismatches.
  - MUST NOT crash the UI when collector sends new fields (forward-compatible).

### DEFINITION OF DONE

- Dashboard renders telemetry exactly as collector provides it.
- Uses only schema-generated types.
- SSE + REST clients are stable, typed, and resilient.
- No local schema drift.
- No dependency violations.
