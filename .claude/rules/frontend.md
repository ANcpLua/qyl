---
paths:
  - "src/qyl.dashboard/**/*.{ts,tsx}"
---

# Frontend Development Rules

## Stack

- Node 22
- React 19
- Vite 7
- Tailwind CSS 4
- TanStack Query 5
- Radix UI
- Recharts
- Lucide React (icons)

## Type Safety

All API types are generated from OpenAPI:
```typescript
import type { TraceListResponse, SpanRecord } from '@/types/api'
```

Regenerate types after OpenAPI changes:
```bash
npm run generate:types
```

## API Client

Use TanStack Query for data fetching:
```typescript
import { useQuery } from '@tanstack/react-query'

const { data, isLoading } = useQuery({
  queryKey: ['traces', filters],
  queryFn: () => fetch('/api/v1/traces').then(r => r.json())
})
```

## Component Guidelines

- Use Radix UI primitives for accessibility
- Tailwind v4 for styling (no `@tailwind` directives)
- Lucide React for icons
- Server-Sent Events for real-time data
