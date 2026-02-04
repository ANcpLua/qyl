# qyl.dashboard - React Frontend

React 19 SPA for telemetry visualization. Embedded in collector at build time.

## Identity

| Property  | Value          |
|-----------|----------------|
| Runtime   | Node 22        |
| Framework | React 19       |
| Build     | Vite 7         |
| Styling   | Tailwind CSS 4 |

## Commands

```bash
# Development server (port 5173)
npm run dev

# Production build
npm run build

# Regenerate TypeScript types from OpenAPI
npm run generate:types
```

## Tech Stack

| Library          | Purpose                   |
|------------------|---------------------------|
| TanStack Query 5 | Data fetching, caching    |
| Radix UI         | Accessible components     |
| Recharts         | Charts and visualizations |
| Lucide React     | Icons                     |

## Directory Structure

```
src/
  components/           # UI components
    sessions/           # Session views
    traces/             # Trace views
    errors/             # Error views
    layout/             # Layout, Sidebar, TopBar
    ui/                 # shadcn/ui primitives
  hooks/                # Custom React hooks
  lib/                  # Utilities, API client
  pages/                # Route components
    GenAIPage.tsx       # GenAI analytics (real data)
    LogsPage.tsx        # Structured logs
    LoginPage.tsx       # Authentication
    ResourcesPage.tsx   # Home/resources view
    SettingsPage.tsx    # Settings
    TracesPage.tsx      # Trace explorer
  types/
    api.ts              # Generated - DO NOT EDIT
  App.tsx               # Main app component
  main.tsx              # Entry point
```

## Data Fetching

```typescript
// TanStack Query for server state
const { data, isLoading, error } = useQuery({
  queryKey: ['sessions', filters],
  queryFn: () => api.getSessions(filters),
  staleTime: 30_000,
});

// Mutations
const mutation = useMutation({
  mutationFn: api.updateError,
  onSuccess: () => queryClient.invalidateQueries(['errors']),
});
```

## SSE Streaming

```typescript
// Real-time updates via Server-Sent Events
useEffect(() => {
  const eventSource = new EventSource('/api/v1/live');

  eventSource.onmessage = (event) => {
    const span = JSON.parse(event.data);
    queryClient.setQueryData(['spans'], (old) => [span, ...old]);
  };

  return () => eventSource.close();
}, []);
```

## API Client

```typescript
// src/lib/api.ts
const api = {
  getSessions: async (filters?: SessionFilters) => {
    const params = new URLSearchParams(filters);
    const res = await fetch(`/api/v1/sessions?${params}`);
    return res.json();
  },

  getTrace: async (traceId: string) => {
    const res = await fetch(`/api/v1/traces/${traceId}`);
    return res.json();
  },
};
```

## Embedding

Build output is copied to collector's wwwroot directory:

```
npm run build
    |
    v
dist/
    |
    v (nuke DashboardEmbed)
src/qyl.collector/wwwroot/
```

The collector serves static files with SPA fallback routing.

## Tailwind CSS 4

Uses Tailwind v4 syntax (no @tailwind directives):

```css
/* src/index.css */
@import "tailwindcss";
```

```tsx
// Component styling
<div className="flex items-center gap-4 p-4 bg-gray-100 dark:bg-gray-800">
  <span className="text-sm text-gray-600">Label</span>
</div>
```

## Rules

- **Never edit** `src/types/api.ts` - it's generated from OpenAPI
- Use Radix UI primitives for accessibility
- Prefer TanStack Query over local state for server data
- Use `useQuery` for reads, `useMutation` for writes
