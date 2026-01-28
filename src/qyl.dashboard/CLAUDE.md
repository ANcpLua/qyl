# qyl.dashboard

React 19 SPA for telemetry visualization.

## identity

```yaml
runtime: Node 22
framework: React 19
build: Vite 7
styling: Tailwind CSS 4
```

## stack

```yaml
state: TanStack Query 5
components: Radix UI
charts: Recharts
icons: Lucide React
types: Generated from OpenAPI
```

## structure

```yaml
src/
  components/    # UI components
  hooks/         # Custom React hooks
  lib/           # Utilities, API client
  types/         # Generated TypeScript types
    api.ts       # DO NOT EDIT - from openapi.yaml
  App.tsx        # Main app component
```

## commands

```yaml
dev: npm run dev
build: npm run build
types: npm run generate:types
```

## embedding

Built output (dist/) is copied to collector/wwwroot/ at build time.
Served as static files by collector with SPA fallback.

## api-consumption

```typescript
// TanStack Query for data fetching
const { data } = useQuery({
  queryKey: ['sessions'],
  queryFn: () => fetch('/api/v1/sessions').then(r => r.json())
})

// SSE for real-time updates
const eventSource = new EventSource('/api/v1/live')
```

## rules

- Never edit src/types/api.ts (generated)
- Use Radix UI for accessibility
- Tailwind v4 syntax (no @tailwind directives)
