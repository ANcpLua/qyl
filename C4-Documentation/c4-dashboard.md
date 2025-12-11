# C4: qyl.dashboard

> React 19 SPA for telemetry visualization with SSE streaming

## Overview

The dashboard is a React 19 TypeScript SPA built with Vite. It provides pages for traces, logs, metrics, GenAI calls,
and resources with real-time SSE updates and virtual scrolling for performance.

## Key Classes/Modules

| Class             | Purpose                       | Location                  |
|-------------------|-------------------------------|---------------------------|
| `TracesPage`      | Waterfall timeline, span tree | `pages/TracesPage.tsx`    |
| `LogsPage`        | SSE streaming, ring buffer    | `pages/LogsPage.tsx`      |
| `MetricsPage`     | Charts, time-series           | `pages/MetricsPage.tsx`   |
| `GenAIPage`       | Token stats, cost tracking    | `pages/GenAIPage.tsx`     |
| `ResourcesPage`   | Session grid/list/graph       | `pages/ResourcesPage.tsx` |
| `SettingsPage`    | Theme, shortcuts, config      | `pages/SettingsPage.tsx`  |
| `useSessions`     | Fetch active sessions         | `hooks/use-telemetry.ts`  |
| `useSessionSpans` | Fetch spans for session       | `hooks/use-telemetry.ts`  |
| `useLiveLogs`     | SSE connection hook           | `pages/LogsPage.tsx`      |
| `RingBuffer<T>`   | O(1) circular buffer          | `lib/RingBuffer.ts`       |
| `cn`              | Tailwind class composition    | `lib/utils.ts`            |

## Dependencies

**Internal:** None (standalone SPA)

**External:** React 19, TanStack Query, TanStack Virtual, Recharts, Lucide Icons, Tailwind CSS

## Data Flow

```
Collector REST API
    ↓
TanStack Query (useSessions, useSessionSpans)
    ↓
Page component state
    ↓
Virtual scrolling (useVirtualizer)
    ↓
Render visible items only

SSE Stream (/api/v1/logs/live)
    ↓
useLiveLogs hook
    ↓
RAF batch → RingBuffer.pushMany()
    ↓
Filter → Virtualize → Render
```

## Patterns Used

- **Ring Buffer**: Logs page uses O(1) circular buffer (10k max)
- **RAF Batching**: SSE messages coalesced per animation frame
- **Virtual Scrolling**: Only visible rows rendered
- **Composite Keys**: LogsPage uses (traceId:timestamp) for stable identity
- **Memoization**: LogRow, ResourceRow wrapped in memo()
