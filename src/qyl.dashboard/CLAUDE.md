# qyl.dashboard - React Frontend

React 19 SPA for telemetry visualization. Embedded in collector via Docker multi-stage build.

## Identity

| Property | Value |
|----------|-------|
| Runtime | Node 22 |
| Framework | React 19 |
| Build | Vite 7 |
| Styling | Tailwind CSS 4 |

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
