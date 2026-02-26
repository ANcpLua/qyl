# qyl.dashboard - React Frontend

Browser surface of qyl. React 19 SPA embedded in the collector — no separate deployment.

## Role in Architecture

One of three shells (browser, terminal, IDE). The dashboard is two things:

1. **Viewer** — telemetry visualization (traces, spans, metrics, errors, GenAI sessions)
2. **Configurator** — select what to observe, the source generator in the customer's local project generates only what's
   selected (incremental, strictly typed, OTel 1.39)

## ADR Implementation: Agents Dashboard

Full spec: `PROMPT-AGENTS-DASHBOARD.md` — pixel-level reference for the `/agents` route.

Key deliverables:
- **Overview tab**: 6 synchronized panels (Traffic, Duration, Issues, LLM Calls, Tokens, Tool Calls)
- **Trace list table**: 9 columns (trace ID, agents, duration, errors, LLM calls, tool calls, tokens, cost, timestamp)
- **Abbreviated trace view**: 60% slide-in panel with AI span waterfall + span detail
- **Models tab**: model-level analytics (calls, tokens, cost, duration, error rate)
- **Tools tab**: tool usage analytics (calls, avg duration, error rate, top agents)

Design requirements:
- Dark theme (#0a0a0f), purple accent, red errors, green success
- Skeleton placeholders (not spinners), staggered fade-in (50ms)
- Compact notation: "355m" tokens, "$0.0151" cost, "2.15min" duration
- Use `semconv.ts` constants — never hardcode attribute strings
- TanStack Query with filter-aware cache keys (from/to/project/env/search)

## ADR-002: Onboarding

First visit (no GitHub token) → "Connect GitHub to get started" screen.
With token → full dashboard. OTLP ingestion always works regardless of auth.

**Upcoming**: Error pages (fingerprinted + grouped errors), deploy correlation views, SLO burn rate dashboard.

## Identity

| Property  | Value          |
|-----------|----------------|
| Runtime   | Node 22        |
| Framework | React 19       |
| Build     | Vite 7         |
| Styling   | Tailwind CSS 4 |

## Commands

```bash
npm run dev              # Dev server (5173)
npm run build            # Production build
npm run generate:types   # Regen TS types from OpenAPI
```

## Stack

TanStack Query 5 | Radix UI | Recharts | Lucide React

## Structure

```
src/
  components/       # UI components (sessions, traces, errors, layout, ui)
  components/agents/  # Agents dashboard components (overview panels, trace detail, waterfall)
  hooks/            # Custom React hooks (use-analytics.ts for agents queries)
  lib/              # Utilities, API client, semconv.ts (generated attribute constants)
  pages/            # Route components (GenAI, Logs, Traces, Resources, Settings, Agents)
  types/api.ts      # Generated — DO NOT EDIT
```

## Rules

- Never edit `src/types/api.ts` — generated from OpenAPI
- Never edit `src/lib/semconv.ts` — generated from OTel semantic conventions
- Radix UI for accessibility
- TanStack Query for server state, not local state
- Recharts for all chart components

## Verification

Use Playwright MCP (`mcp__playwright`, browser: msedge) to verify:
- Dashboard loads at localhost:5100
- `/agents` route renders all 6 overview panels
- Trace list table shows correct columns
- Click trace → slide-in panel with waterfall
