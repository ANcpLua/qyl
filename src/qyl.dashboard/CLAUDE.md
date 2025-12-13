# qyl.dashboard

@import "../../CLAUDE.md"

## Scope

React frontend for viewing sessions, spans and traces. Talks to `qyl.collector` over REST + SSE only.

## Type Source Rules

Types in `src/qyl.dashboard/src/types/generated/` are generated (Kiota). Do NOT edit them manually.

Correct:
```ts
import type { SessionDto } from "@/types/generated/models/qyl/sessionDto";
```

Wrong (manual duplication):
```ts
type SessionDto = { /* ... */ };
```

## SSE Pattern

Prefer a single, reconnecting EventSource hook and invalidate TanStack Query caches on new events.

## Forbidden Actions

- Do not edit files under `src/qyl.dashboard/src/types/generated/`
- Do not import from any .NET project
- Do not introduce `any` types for API shapes

## Commands

```bash
npm run dev --prefix src/qyl.dashboard
npm run build --prefix src/qyl.dashboard
```
