# qyl.dashboard - React Frontend

Browser surface of qyl. React 19 SPA embedded in the collector — no separate deployment, no login, no account creation.

## Role in Architecture

One of three shells (browser, terminal, IDE). The dashboard is two things:

1. **Viewer** — telemetry visualization (traces, spans, metrics, errors, GenAI sessions)
2. **Configurator** — select what to observe, the source generator in the customer's local project generates only what's
   selected (incremental, strictly typed, OTel 1.39)

No login wall. Server auto-detects the token handshake from the browser. Customer sees value before they know they have
an account.

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
  hooks/            # Custom React hooks
  lib/              # Utilities, API client
  pages/            # Route components (GenAI, Logs, Traces, Resources, Settings)
  types/api.ts      # Generated — DO NOT EDIT
```

## Rules

- Never edit `src/types/api.ts` — generated from OpenAPI
- Radix UI for accessibility
- TanStack Query for server state, not local state
