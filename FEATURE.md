# qyl. Architecture & Implementation Status

## 1. Architecture (Current State)
````mermaid
graph TD
    subgraph "Telemetry Sources"
        App[Application] -- "OTLP/gRPC :4317" --> gRPC
        App -- "OTLP/HTTP :4318" --> HTTP
    end

    subgraph "qyl.grpc (Library)"
        gRPC[OtlpTraceService]
        HTTP[OtlpHttpHandler]
        Conv[OtlpConverter]
        Extract[GenAiExtractor]
        
        gRPC --> Conv
        HTTP --> Conv
        Conv --> Extract
    end

    subgraph "qyl.collector (.NET 10)"
        Store[InMemory / DuckDB]
        TraceAgg[TraceAggregator]
        SessionAgg[SessionAggregator]
        SSE[SSE Stream]
        API[REST API :5000]
        
        Extract --> Store
        Extract --> TraceAgg
        Extract --> SessionAgg
        Store --> API
        SessionAgg --> API
        TraceAgg --> SSE
    end

    subgraph "Storage"
        Memory[(InMemoryStore)]
        DuckDB[(DuckDB)]
        Store --> Memory
        Store --> DuckDB
    end

    subgraph "Frontend"
        Dash[qyl.dashboard :5173]
    end

    Dash -- "GET /api/v1/sessions" --> API
    Dash -- "GET /api/v1/sessions/stats" --> API
    Dash -- "SSE /api/v1/live" --> SSE
````

## 2. Component Status

| Component | Location | Status | Notes |
|-----------|----------|--------|-------|
| OTLP gRPC Receiver | qyl.grpc | ✅ Complete | Traces, Metrics, Logs |
| OTLP HTTP Receiver | qyl.grpc | ✅ Complete | Protobuf + JSON |
| OtlpConverter | qyl.grpc | ✅ Complete | Full proto parsing |
| GenAiExtractor | qyl.grpc | ✅ Complete | Denormalizes gen_ai.* |
| SessionAggregator | qyl.grpc | ✅ Complete | Groups by session/trace |
| TraceAggregator | qyl.grpc | ✅ Complete | Span → Trace |
| InMemoryStore | qyl.grpc | ✅ Complete | Ring buffer, 10k default |
| TelemetryBroadcaster | qyl.grpc | ✅ Complete | Channel-based pub/sub |
| SSE Endpoints | qyl.grpc | ✅ Complete | .NET 10 native |
| Sessions API | qyl.grpc | ✅ Complete | /api/v1/sessions |
| DuckDbStore | qyl.grpc | ✅ Complete | Columnar with gen_ai.* |
| Dashboard Types | qyl.dashboard | ✅ Complete | TypeScript interfaces |
| Dashboard Hooks | qyl.dashboard | ✅ Complete | TanStack Query |
| SSE Client | qyl.dashboard | ✅ Complete | Auto-reconnect |

## 3. Port Summary

| Port | Service | Protocol |
|------|---------|----------|
| 4317 | qyl.collector | OTLP gRPC |
| 4318 | qyl.collector | OTLP HTTP |
| 5000 | qyl.collector | REST API + SSE |
| 5173 | qyl.dashboard | Vite Dev Server |

## 4. API Endpoints

### Sessions API
````
GET  /api/v1/sessions              → SessionListResponse
GET  /api/v1/sessions/{id}         → SessionDetailResponse
GET  /api/v1/sessions/stats        → SessionStatistics
````

### Telemetry API
````
GET  /api/v1/traces                → TraceListResponse
GET  /api/v1/traces/{id}           → TraceDetailResponse
GET  /api/v1/services              → ServiceListResponse
GET  /api/v1/logs                  → LogListResponse
GET  /api/v1/metrics               → MetricListResponse
````

### Streaming
````
GET  /api/v1/live                  → SSE (all signals)
GET  /api/v1/live/spans            → SSE (spans only)
GET  /api/v1/live/metrics          → SSE (metrics only)
GET  /api/v1/live/logs             → SSE (logs only)
````

### OTLP Ingestion
````
POST /v1/traces                    → OTLP HTTP (protobuf/json)
POST /v1/metrics                   → OTLP HTTP (protobuf/json)
POST /v1/logs                      → OTLP HTTP (protobuf/json)
gRPC :4317                         → OTLP gRPC (all signals)
````

## 5. Frontend Utilities

### src/lib/otel.ts
````typescript
import type { Span } from '@/types/telemetry';

// Model pricing (USD per 1K tokens)
const PRICING: Record<string, { in: number; out: number }> = {
  'gpt-4o': { in: 0.005, out: 0.015 },
  'gpt-4o-mini': { in: 0.00015, out: 0.0006 },
  'gpt-4-turbo': { in: 0.01, out: 0.03 },
  'gpt-3.5-turbo': { in: 0.0005, out: 0.0015 },
  'claude-3-5-sonnet': { in: 0.003, out: 0.015 },
  'claude-3-opus': { in: 0.015, out: 0.075 },
  'claude-3-haiku': { in: 0.00025, out: 0.00125 },
  'claude-sonnet-4': { in: 0.003, out: 0.015 },
  'claude-opus-4': { in: 0.015, out: 0.075 },
  'gemini-1.5-pro': { in: 0.00125, out: 0.005 },
  'gemini-1.5-flash': { in: 0.000075, out: 0.0003 },
  'default': { in: 0, out: 0 },
};

export type Attributes = Record<string, unknown>;

/**
 * Parse attributes from backend.
 * Handles both JSON string (C# serialization quirk) and object.
 */
export function parseAttributes(attrs: string | Attributes | undefined): Attributes {
  if (!attrs) return {};
  if (typeof attrs === 'object') return attrs;
  try {
    return JSON.parse(attrs);
  } catch {
    return {};
  }
}

/**
 * Extract gen_ai.* data from span.
 * Prefers explicit backend fields, falls back to attributes.
 */
export function parseGenAIData(span: Span & {
  genAiSystem?: string;
  genAiModel?: string;
  inputTokens?: number;
  outputTokens?: number;
  costUsd?: number;
}) {
  const attrs = parseAttributes(span.attributes as string | Attributes);

  const provider = span.genAiSystem 
    ?? String(attrs['gen_ai.system'] || 'unknown');
  
  const model = span.genAiModel 
    ?? String(attrs['gen_ai.request.model'] || attrs['gen_ai.response.model'] || 'unknown');
  
  const input = span.inputTokens 
    ?? Number(attrs['gen_ai.usage.input_tokens'] || attrs['gen_ai.usage.prompt_tokens'] || 0);
  
  const output = span.outputTokens 
    ?? Number(attrs['gen_ai.usage.output_tokens'] || attrs['gen_ai.usage.completion_tokens'] || 0);

  // Calculate cost if not provided by backend
  let cost = span.costUsd;
  if (cost === undefined || cost === null) {
    const modelKey = Object.keys(PRICING).find(k => 
      model.toLowerCase().includes(k.toLowerCase())
    );
    const rate = PRICING[modelKey ?? 'default'];
    cost = (input / 1000 * rate.in) + (output / 1000 * rate.out);
  }

  return {
    provider,
    model,
    input,
    output,
    cost,
    totalTokens: input + output,
  };
}

/**
 * Check if span is a gen_ai span.
 */
export function isGenAISpan(span: Span): boolean {
  if ('genAiSystem' in span && span.genAiSystem) return true;
  if ('genAiModel' in span && span.genAiModel) return true;
  
  const attrs = parseAttributes(span.attributes as string | Attributes);
  return !!(attrs['gen_ai.system'] || attrs['gen_ai.request.model']);
}

/**
 * Format token count for display.
 */
export function formatTokens(count: number): string {
  if (count >= 1_000_000) return `${(count / 1_000_000).toFixed(1)}M`;
  if (count >= 1_000) return `${(count / 1_000).toFixed(1)}K`;
  return count.toString();
}

/**
 * Format cost for display.
 */
export function formatCost(usd: number): string {
  if (usd >= 1) return `$${usd.toFixed(2)}`;
  if (usd >= 0.01) return `$${usd.toFixed(3)}`;
  return `$${usd.toFixed(4)}`;
}
````

## 6. CLAUDE.md Additions

Add to project `CLAUDE.md`:
````markdown
## qyl. Architecture

### Stack
- **Backend:** .NET 10 (qyl.grpc library + qyl.collector host)
- **Frontend:** React 19, TypeScript, TanStack Query, Tailwind v4
- **Storage:** DuckDB (columnar) or InMemory (ring buffer)
- **Protocol:** OTLP v1.38 (gRPC + HTTP)

### Key Patterns

1. **GenAiExtractor** denormalizes `gen_ai.*` attributes server-side
   - Frontend receives typed fields: `genAiSystem`, `genAiModel`, `inputTokens`, etc.
   - Fallback: parse `attributes` JSON string if typed fields missing

2. **Sessions vs Traces**
   - Session = conversation (grouped by `qyl.session.id` or `gen_ai.conversation.id`)
   - Trace = single request (grouped by `trace_id`)
   - Sessions contain multiple traces

3. **SSE Streaming**
   - Uses .NET 10 native `TypedResults.ServerSentEvents`
   - Events: `connected`, `spans`, `metrics`, `logs`
   - Auto-reconnect with exponential backoff

### API Quick Reference

| Endpoint | Returns |
|----------|---------|
| `GET /api/v1/sessions` | AI conversation list with token/cost rollup |
| `GET /api/v1/sessions/{id}` | Session detail with all spans |
| `GET /api/v1/sessions/stats` | Aggregate statistics |
| `GET /api/v1/live` | SSE stream (all signals) |
| `POST /v1/traces` | OTLP HTTP ingestion |

### gen_ai.* Semantic Conventions

| Attribute | Type | Description |
|-----------|------|-------------|
| `gen_ai.system` | string | Provider (openai, anthropic, etc.) |
| `gen_ai.request.model` | string | Model name |
| `gen_ai.usage.input_tokens` | int | Prompt tokens |
| `gen_ai.usage.output_tokens` | int | Completion tokens |
| `gen_ai.response.finish_reason` | string | stop, length, tool_calls |

### qyl.* Extension Attributes

| Attribute | Type | Description |
|-----------|------|-------------|
| `qyl.session.id` | string | Conversation grouping key |
| `qyl.cost.usd` | decimal | Actual cost (if known) |
| `qyl.feedback.score` | int | User rating (-1, 0, 1) |
| `qyl.agent.id` | string | Agent identifier |

### DuckDB Schema (Denormalized)
```sql
-- Spans table has typed gen_ai columns for fast queries
SELECT 
  session_id,
  SUM(gen_ai_input_tokens) as total_input,
  SUM(gen_ai_output_tokens) as total_output,
  SUM(gen_ai_cost_usd) as total_cost
FROM spans
WHERE gen_ai_system IS NOT NULL
GROUP BY session_id;
```

### Anti-Patterns

❌ Don't use Entity Framework with DuckDB (raw SQL only)
❌ Don't parse attributes client-side if backend provides typed fields
❌ Don't use WebSocket for SSE (use native EventSource)
❌ Don't hardcode mock data in pages
❌ Don't add auth yet (scope creep)
````

## 7. Remaining Work

| Task | Priority | Effort |
|------|----------|--------|
| Wire dashboard to real APIs | P0 | 30min |
| Delete LiveTail.tsx | P0 | 5min |
| Add /health endpoint | P1 | 5min |
| Test with real OTLP exporter | P1 | 30min |
| Add Dockerfile | P2 | 15min |
| Cold tier (Parquet export) | P3 | 2h |

## 8. Test Commands
````bash
# Start collector
cd src/qyl.collector && dotnet run

# Send test spans (requires otel-cli or curl)
curl -X POST http://localhost:4318/v1/traces \
  -H "Content-Type: application/json" \
  -d '{"resourceSpans":[...]}'

# Check sessions
curl http://localhost:5000/api/v1/sessions

# Check stats
curl http://localhost:5000/api/v1/sessions/stats

# SSE stream
curl -N http://localhost:5000/api/v1/live
````