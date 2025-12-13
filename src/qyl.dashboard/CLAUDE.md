# qyl.dashboard — Frontend

@../../CLAUDE.md

## Purpose

React SPA for visualizing qyl telemetry:
- Sessions list with filtering
- Trace tree explorer
- GenAI analytics (token usage, costs, models)
- Real-time updates via SSE

**Separately deployable** — collector does NOT bundle the dashboard.

## Hard Rules

| ✅ MAY Reference | ❌ MUST NOT Reference |
|-----------------|----------------------|
| `react`, `react-dom` | Any .NET project |
| `@tanstack/react-query` | Any backend code |
| `@tanstack/react-virtual` | DuckDB |
| `recharts` | Protobuf/gRPC |
| `tailwindcss` | |

**Critical**: Dashboard is a pure frontend. Communicates with collector via HTTP only.

## Structure

```
qyl.dashboard/
├── qyl.dashboard.esproj          # VS integration (optional)
├── package.json
├── vite.config.ts
├── tsconfig.json
├── tailwind.config.ts
├── src/
│   ├── main.tsx                  # Entry point
│   ├── App.tsx                   # Router setup
│   ├── api/
│   │   ├── client.ts             # Fetch wrapper
│   │   └── hooks.ts              # TanStack Query hooks
│   ├── hooks/
│   │   └── useSse.ts             # SSE streaming
│   ├── components/
│   │   ├── layout/
│   │   │   ├── Sidebar.tsx
│   │   │   └── Header.tsx
│   │   ├── sessions/
│   │   │   ├── SessionList.tsx
│   │   │   └── SessionCard.tsx
│   │   ├── traces/
│   │   │   ├── TraceTree.tsx
│   │   │   └── SpanDetail.tsx
│   │   └── genai/
│   │       ├── TokenChart.tsx
│   │       ├── ModelUsage.tsx
│   │       └── CostBreakdown.tsx
│   ├── pages/
│   │   ├── SessionsPage.tsx
│   │   ├── TracesPage.tsx
│   │   ├── GenAiPage.tsx
│   │   └── SettingsPage.tsx
│   └── types/
│       └── api.ts                # TypeScript types
├── public/
└── Dockerfile
```

## Technology Stack

| Tool | Version | Purpose |
|------|---------|---------|
| React | 19.x | UI framework |
| Vite | 6.x | Build tool |
| TanStack Query | 5.x | Data fetching |
| TanStack Virtual | 3.x | Virtualized lists |
| Tailwind CSS | 4.x | Styling |
| Recharts | 2.x | Charts |
| React Router | 7.x | Routing |
| TypeScript | 5.7+ | Type safety |

## API Communication

### Hooks (TanStack Query)

```typescript
// src/api/hooks.ts
export function useSessions(limit = 50) {
  return useQuery({
    queryKey: ['sessions', limit],
    queryFn: () => client.get<SessionSummary[]>(`/api/v1/sessions?limit=${limit}`),
    refetchInterval: 5000,  // Poll every 5s
  });
}

export function useSpans(filters: SpanFilters) {
  const params = new URLSearchParams();
  if (filters.serviceName) params.set('serviceName', filters.serviceName);
  if (filters.from) params.set('from', filters.from);
  if (filters.to) params.set('to', filters.to);
  if (filters.genAiOnly) params.set('genAiOnly', 'true');
  params.set('limit', String(filters.limit ?? 100));

  return useQuery({
    queryKey: ['spans', filters],
    queryFn: () => client.get<SpanRecord[]>(`/api/v1/spans?${params}`),
  });
}

export function useTrace(traceId: string | undefined) {
  return useQuery({
    queryKey: ['trace', traceId],
    queryFn: () => client.get<TraceNode>(`/api/v1/traces/${traceId}`),
    enabled: !!traceId,
  });
}
```

### SSE Streaming

```typescript
// src/hooks/useSse.ts
export function useSpanStream() {
  const queryClient = useQueryClient();
  const eventSourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    const es = new EventSource('/api/v1/events/spans');
    eventSourceRef.current = es;

    es.onmessage = (event) => {
      const span = JSON.parse(event.data);
      queryClient.invalidateQueries({ queryKey: ['sessions'] });
      queryClient.invalidateQueries({ queryKey: ['spans'] });
    };

    es.onerror = () => {
      es.close();
      // Reconnect after 3s
      setTimeout(() => {
        eventSourceRef.current = new EventSource('/api/v1/events/spans');
      }, 3000);
    };

    return () => es.close();
  }, [queryClient]);
}
```

## Types

Keep TypeScript types in sync with `qyl.protocol` C# models:

```typescript
// src/types/api.ts
export interface SessionSummary {
  sessionId: string;
  serviceName: string;
  startTime: string;  // ISO 8601
  endTime: string;
  traceCount: number;
  spanCount: number;
  errorCount: number;
  genAiSpanCount: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  primaryModel?: string;
  primaryProvider?: string;
}

export interface SpanRecord {
  traceId: string;
  spanId: string;
  parentSpanId?: string;
  name: string;
  startTime: string;
  endTime: string;
  status: number;  // 0=Unset, 1=Ok, 2=Error
  statusMessage?: string;
  serviceName?: string;
  genAi?: GenAiSpanData;
}

export interface GenAiSpanData {
  operationName?: string;
  provider?: string;
  requestModel?: string;
  responseModel?: string;
  inputTokens?: number;
  outputTokens?: number;
  temperature?: number;
  finishReason?: string;
}

export interface TraceNode {
  span: SpanRecord;
  children: TraceNode[];
}
```

## Environment Variables

```bash
VITE_API_URL=http://localhost:8080  # Collector URL
```

In code:
```typescript
const API_URL = import.meta.env.VITE_API_URL || '';
```

## Development

```bash
# Install dependencies
pnpm install

# Start dev server (with hot reload)
pnpm dev

# Build for production
pnpm build

# Preview production build
pnpm preview
```

## Docker

```dockerfile
# Build stage
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json pnpm-lock.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile
COPY . .
RUN pnpm build

# Production stage
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

`nginx.conf`:
```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Proxy API requests to collector
    location /api/ {
        proxy_pass http://collector:8080;
    }

    location /v1/ {
        proxy_pass http://collector:8080;
    }
}
```

## Styling Guidelines

Using Tailwind CSS v4:
- Use utility classes directly
- Prefer `flex` and `grid` for layouts
- Use `dark:` prefix for dark mode
- Component variants via `class` props, not CSS modules

## Component Patterns

### Virtualized Lists (for large datasets)
```tsx
import { useVirtualizer } from '@tanstack/react-virtual';

function SpanList({ spans }: { spans: SpanRecord[] }) {
  const parentRef = useRef<HTMLDivElement>(null);
  
  const virtualizer = useVirtualizer({
    count: spans.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 60,
  });

  return (
    <div ref={parentRef} className="h-[600px] overflow-auto">
      <div style={{ height: virtualizer.getTotalSize() }}>
        {virtualizer.getVirtualItems().map((virtualItem) => (
          <SpanRow key={virtualItem.key} span={spans[virtualItem.index]} />
        ))}
      </div>
    </div>
  );
}
```

### Loading/Error States
```tsx
function SessionsPage() {
  const { data, isLoading, error } = useSessions();

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorMessage error={error} />;
  if (!data?.length) return <EmptyState message="No sessions yet" />;

  return <SessionList sessions={data} />;
}
```
