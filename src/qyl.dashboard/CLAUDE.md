# qyl.dashboard

Inherits: [Root CLAUDE.md](../../CLAUDE.md)

React 19 SPA for viewing sessions, spans, and traces. Communicates with `qyl.collector` via REST + SSE.

## Architecture

```
qyl.dashboard ──HTTP/SSE──► qyl.collector:5100
     │
     └──► /api/v1/sessions, /api/v1/traces, /api/v1/live
```

## Ports and Proxy

| Service   | Port | Notes                              |
|-----------|------|------------------------------------|
| Dashboard | 5173 | Vite dev server                    |
| Collector | 5100 | Proxied via `/api/*` in dev        |

Proxy configured in `vite.config.ts` - uses `VITE_API_URL` env var or defaults to `http://localhost:5100`.

## Type Generation (God Schema)

Types flow from TypeSpec through OpenAPI to TypeScript:

```
core/specs/main.tsp (SSOT)
     │
     └─► core/openapi/openapi.yaml
              │
              └─► openapi-typescript
                       │
                       └─► src/types/api.ts (generated)
                                │
                                └─► src/types/index.ts (re-exports + utilities)
```

### Generated vs Manual Files

| File | Type | Source |
|------|------|--------|
| `src/types/api.ts` | Generated | OpenAPI schema via `npm run generate:ts` |
| `src/types/index.ts` | Manual | Re-exports + utility functions |

### Type Usage

Components use `SpanRecord` directly from OpenAPI with utility functions:

```typescript
// Import types and utilities
import type { SpanRecord, StatusCode } from '@/types';
import { nsToMs, nanoToIso, getAttributes, getTotalTokens, STATUS_ERROR } from '@/types';

// Use SpanRecord fields directly
const durationMs = nsToMs(span.durationNs);
const startTime = nanoToIso(span.startTimeUnixNano);
const attrs = getAttributes(span);  // Parses attributesJson
const isError = span.statusCode === STATUS_ERROR;

// Access GenAI fields directly (flat, not nested)
span.genAiSystem          // Provider name (openai, anthropic)
span.genAiRequestModel    // Requested model
span.genAiInputTokens     // Token counts
span.genAiCostUsd         // Cost
```

Import from `@/types` only:

```typescript
// Correct - use SpanRecord directly
import type { SpanRecord, Session } from '@/types';
import { nsToMs, getAttributes, getTotalTokens } from '@/types';

// Wrong - direct api.ts import
import type { components } from '@/types/api';
```

## SSE Live Stream

Single reconnecting EventSource with TanStack Query cache invalidation:

```typescript
// In hooks/use-telemetry.ts
const { isConnected, recentSpans, reconnect } = useLiveStream({
    sessionFilter: sessionId,
    onSpans: (batch) => { /* handle */ },
});
```

Key behaviors:
- Auto-reconnects on disconnect (3s delay)
- Maintains last 100 spans in memory
- Invalidates `sessions` query on new spans

## Key Components

| Component         | Purpose                           |
|-------------------|-----------------------------------|
| `LiveTail.tsx`    | Real-time span stream display     |
| `GenAIPage.tsx`   | GenAI-specific analytics          |
| `TracesPage.tsx`  | Trace tree visualization          |
| `SessionsPage.tsx`| Session list with filters         |

## Forbidden Actions

- Do NOT edit `src/types/api.ts` - regenerate from OpenAPI
- Do NOT import from .NET projects
- Do NOT duplicate types that exist in `api.ts`
- Do NOT use `any` for API response shapes

## Known Issues

### Security Vulnerabilities

Run `npm audit fix` to address:
- `@modelcontextprotocol/sdk` ReDoS (GHSA-8r9q-7v3j-jr4g)
- `qs` DoS via memory exhaustion (GHSA-6rw7-vpxm-498p)

### Missing Features

- No health check indicator for collector connection status

## Commands

```bash
npm run dev          # Start dev server (port 5173)
npm run build        # Production build
npm run typecheck    # TypeScript validation
npm run lint         # ESLint check
npm run test         # Vitest tests
npm run generate:ts  # Regenerate types from OpenAPI
```
