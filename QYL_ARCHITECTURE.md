# qyl. Architecture Specification

> **The single source of truth for qyl. implementation. No alternatives. No exceptions.**

---

## Core Rules (Enforce Strictly)

```
qyl. rules:
- Dashboard consumes ONLY from qyl.collector (/api/v1/query + /api/v1/live)
- Apps produce ONLY via qyl.sdk → qyl.collector (native protocol)
- No direct app ↔ dashboard communication ever
- OTLP is only an ingress shim, never mentioned as primary
- Answer with exactly one path. Always decisive.
```

---

## The Architecture (Only Way It Exists)

```
┌───────────────────────────────────────────────────────────────────────────────┐
│                              qyl. ECOSYSTEM                                    │
├───────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                         PRODUCING (App Side)                             │ │
│  │                                                                          │ │
│  │   User Code                        qyl.sdk                               │ │
│  │   ┌──────────────────┐            ┌────────────────────────────────┐    │ │
│  │   │ var session =    │            │ • Qyl.Session()                │    │ │
│  │   │   Qyl.Session(); │ ─────────► │ • .Span()                      │    │ │
│  │   │                  │            │ • .WithCost()                  │    │ │
│  │   │ session.Span()   │            │ • .WithTokens()                │    │ │
│  │   │   .WithCost()    │            │ • .WithEval()                  │    │ │
│  │   │   .WithTokens(); │            │ • .AddFeedback()               │    │ │
│  │   └──────────────────┘            │ • Built-in native exporter     │    │ │
│  │                                   └─────────────┬──────────────────┘    │ │
│  └─────────────────────────────────────────────────┼────────────────────────┘ │
│                                                    │                          │
│                                    qyl. native protocol (protobuf/JSON)       │
│                                    (NOT OTLP as primary)                      │
│                                                    │                          │
│                                                    ▼                          │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                         COLLECTING (qyl.collector)                       │ │
│  │                         Native AOT, ~25MB, single binary                 │ │
│  │                                                                          │ │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────────┐  │ │
│  │   │ qyl. native  │  │ OTLP shim    │  │ Static Files                 │  │ │
│  │   │ :5100        │  │ :4317/:4318  │  │ (qyl.dashboard bundle)       │  │ │
│  │   │ (PRIMARY)    │  │ (compat only)│  │                              │  │ │
│  │   └──────┬───────┘  └──────┬───────┘  └──────────────────────────────┘  │ │
│  │          │                 │                                             │ │
│  │          ▼                 ▼                                             │ │
│  │   ┌─────────────────────────────────────────────────────────────────┐   │ │
│  │   │              Schema Normalizer                                   │   │ │
│  │   │  • semconv 1.28 → 1.38 transforms                               │   │ │
│  │   │  • qyl. analyzers validate at build time                        │   │ │
│  │   └─────────────────────────┬───────────────────────────────────────┘   │ │
│  │                             │                                            │ │
│  │                             ▼                                            │ │
│  │   ┌─────────────────────────────────────────────────────────────────┐   │ │
│  │   │                    DuckDB (embedded columnar)                    │   │ │
│  │   │  • spans.duckdb (hot, last 7 days)                              │   │ │
│  │   │  • Parquet export (cold, S3/local)                              │   │ │
│  │   │  • 100M+ spans on single machine                                │   │ │
│  │   └─────────────────────────────────────────────────────────────────┘   │ │
│  │                                                                          │ │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────────┐  │ │
│  │   │ Query API    │  │ SSE Stream   │  │ MCP Server                   │  │ │
│  │   │/api/v1/query │  │/api/v1/live  │  │ (stdio transport)            │  │ │
│  │   └──────────────┘  └──────────────┘  └──────────────────────────────┘  │ │
│  │                                                                          │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
│                                                    │                          │
│                              ONLY these endpoints  │                          │
│                                                    ▼                          │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                         CONSUMING (qyl.dashboard)                        │ │
│  │                         SolidJS + TanStack Query                         │ │
│  │                                                                          │ │
│  │   GET /api/v1/query ◄─────── Trace browser, search, filters             │ │
│  │   GET /api/v1/live  ◄─────── SSE live tail, real-time spans             │ │
│  │                                                                          │ │
│  │   NEVER talks to apps directly. NEVER.                                  │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
│                                                                               │
└───────────────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure (The Only Way)

```
/qyl
├── /src
│   ├── /qyl.sdk                        # What users import
│   │   ├── Qyl.cs                      # Entry point: Qyl.Session()
│   │   ├── QylSession.cs               # Fluent session builder
│   │   ├── QylSpan.cs                  # Fluent span builder
│   │   ├── QylExporter.cs              # Native protocol exporter (built-in)
│   │   ├── /Extensions
│   │   │   ├── CostExtensions.cs       # .WithCost()
│   │   │   ├── TokenExtensions.cs      # .WithTokens()
│   │   │   ├── EvalExtensions.cs       # .WithEval()
│   │   │   └── FeedbackExtensions.cs   # .AddFeedback()
│   │   └── /Generated                  # From qyl.schema codegen
│   │       └── QylAttributes.g.cs
│   │
│   ├── /qyl.collector                  # The single backend
│   │   ├── Program.cs                  # Slim builder, Native AOT
│   │   ├── /Ingestion
│   │   │   ├── QylNativeEndpoint.cs    # PRIMARY: qyl. protocol
│   │   │   ├── OtlpShim.cs             # Compat only, transforms to native
│   │   │   └── SchemaNormalizer.cs     # 1.28 → 1.38
│   │   ├── /Storage
│   │   │   ├── DuckDbStore.cs          # Hot storage
│   │   │   ├── ParquetExporter.cs      # Cold tier
│   │   │   └── Schema.sql
│   │   ├── /Query
│   │   │   └── QueryService.cs
│   │   ├── /Realtime
│   │   │   └── SseHub.cs               # Live tail
│   │   └── /Mcp
│   │       └── QylMcpServer.cs
│   │
│   ├── /qyl.dashboard                  # SolidJS frontend
│   │   ├── package.json
│   │   ├── /src
│   │   │   ├── App.tsx
│   │   │   ├── /components
│   │   │   │   ├── TraceWaterfall.tsx  # Canvas-based
│   │   │   │   ├── SpanDetail.tsx
│   │   │   │   ├── LiveTail.tsx
│   │   │   │   └── FeedbackPanel.tsx
│   │   │   └── /api
│   │   │       └── client.ts           # ONLY talks to /api/v1/*
│   │   └── vite.config.ts
│   │
│   ├── /qyl.schema                     # Schema definition + codegen
│   │   ├── qyl.schema.yaml             # THE source of truth
│   │   ├── /Codegen
│   │   │   └── SdkGenerator.cs         # Scriban + YamlDotNet
│   │   └── /Templates
│   │       ├── CSharpSdk.scriban
│   │       └── TypeScriptSdk.scriban
│   │
│   ├── /qyl.demo                       # Example app (existing)
│   │
│   └── /qyl.providers.gemini           # IChatClient providers
│
├── /eng
│   ├── /qyl.analyzers                  # Roslyn analyzers
│   │   ├── QylGenAiDeprecatedAttributeAnalyzer.cs
│   │   └── QylGenAiNonCanonicalAttributeAnalyzer.cs
│   ├── /qyl.cli                        # CLI tooling
│   └── /MSBuild
│       ├── Shared.props
│       ├── Shared.targets
│       └── LegacySupport.targets
│
├── /examples
│   └── /qyl.analyzers.Sample
│
├── /tests
│   └── /UnitTests
│
├── qyl.slnx
├── CLAUDE.md
└── QYL_ARCHITECTURE.md                 # This file
```

---

## The SDK API (What Users Write)

### C# Example (This Is Literally All)

```csharp
using Qyl;  // ← your extension methods only

// Start a session (groups related spans)
var session = Qyl.Session("customer-support-v3");

// Create spans with fluent API
await session.Span("user_query")
    .WithInput(message)
    .CallAsync(async () =>
    {
        return await chatClient.CompleteAsync(message);
    })
    .WithCost(0.023m)
    .WithTokens(input: 412, output: 189);

// Nested tool calls
await session.Span("tool_execution")
    .WithTool("search_docs")
    .CallAsync(async () => await SearchDocs(query));

// Feedback (can happen minutes/hours later)
session.AddFeedback(Feedback.ThumbsDown, "hallucinated the return policy");
```

### TypeScript Example

```typescript
import { Qyl } from '@qyl/sdk';

const session = Qyl.session('customer-support-v3');

await session.span('user_query')
  .withInput(message)
  .call(async () => {
    return await chatClient.complete(message);
  })
  .withCost(0.023)
  .withTokens({ input: 412, output: 189 });

session.addFeedback('thumbs_down', 'hallucinated the return policy');
```

---

## qyl.schema Definition (Source of Truth)

```yaml
# qyl.schema.yaml - generates all SDK code
version: "1.38"

session:
  attributes:
    - name: session.id
      type: string
      required: true
    - name: session.name
      type: string
    - name: session.user_id
      type: string

span:
  attributes:
    # Core
    - name: span.name
      type: string
      required: true
    - name: span.kind
      type: enum
      values: [agent, tool, llm, retrieval, embedding]

    # GenAI (1.38 canonical names)
    - name: gen_ai.provider.name
      type: string
    - name: gen_ai.request.model
      type: string
    - name: gen_ai.usage.input_tokens
      type: int
    - name: gen_ai.usage.output_tokens
      type: int

    # qyl. extensions (our own)
    - name: qyl.cost.usd
      type: decimal
    - name: qyl.eval.score
      type: float
    - name: qyl.eval.reason
      type: string

feedback:
  attributes:
    - name: feedback.type
      type: enum
      values: [thumbs_up, thumbs_down, correction, rating]
    - name: feedback.value
      type: string
    - name: feedback.comment
      type: string
    - name: feedback.timestamp
      type: timestamp
```

---

## DuckDB Schema

```sql
-- sessions table
CREATE TABLE sessions (
    session_id VARCHAR PRIMARY KEY,
    name VARCHAR,
    user_id VARCHAR,
    started_at TIMESTAMPTZ NOT NULL,
    ended_at TIMESTAMPTZ,
    metadata JSON
);

-- spans table (hot storage)
CREATE TABLE spans (
    trace_id VARCHAR NOT NULL,
    span_id VARCHAR NOT NULL,
    parent_span_id VARCHAR,
    session_id VARCHAR REFERENCES sessions(session_id),

    name VARCHAR NOT NULL,
    kind VARCHAR,  -- agent, tool, llm, retrieval, embedding
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ NOT NULL,
    duration_ms DOUBLE GENERATED ALWAYS AS (
        EXTRACT(EPOCH FROM (end_time - start_time)) * 1000
    ),
    status_code INT8,
    status_message VARCHAR,

    -- GenAI semantic conventions (1.38)
    provider_name VARCHAR,           -- gen_ai.provider.name
    request_model VARCHAR,           -- gen_ai.request.model
    tokens_in INT,                   -- gen_ai.usage.input_tokens
    tokens_out INT,                  -- gen_ai.usage.output_tokens

    -- qyl. extensions
    cost_usd DECIMAL(10,6),
    eval_score FLOAT,
    eval_reason VARCHAR,

    -- Flexible storage
    attributes JSON,
    events JSON,

    PRIMARY KEY (trace_id, span_id)
);

-- feedback table (linked to sessions, can arrive later)
CREATE TABLE feedback (
    feedback_id VARCHAR PRIMARY KEY,
    session_id VARCHAR NOT NULL REFERENCES sessions(session_id),
    span_id VARCHAR,  -- optional, can be session-level

    type VARCHAR NOT NULL,  -- thumbs_up, thumbs_down, correction, rating
    value VARCHAR,
    comment VARCHAR,
    created_at TIMESTAMPTZ NOT NULL,

    metadata JSON
);

-- Indexes
CREATE INDEX idx_spans_time ON spans (start_time);
CREATE INDEX idx_spans_session ON spans (session_id);
CREATE INDEX idx_spans_kind ON spans (kind) WHERE kind IS NOT NULL;
CREATE INDEX idx_feedback_session ON feedback (session_id);
```

---

## Component Responsibilities

| Component | Responsibility | Never Does |
|-----------|---------------|------------|
| **qyl.sdk** | Fluent API, span creation, native export to collector | Direct OTel, direct dashboard calls |
| **qyl.collector** | Receive, normalize, store, serve | Run in app process, talk to external DBs |
| **qyl.dashboard** | Visualize, query, feedback UI | Direct SDK calls, direct DB access |
| **qyl.schema** | Define attributes, generate SDK code | Runtime logic |
| **qyl.analyzers** | Build-time validation of attributes | Runtime checks |

---

## API Endpoints (qyl.collector)

### Query API
```
GET  /api/v1/sessions                    # List sessions
GET  /api/v1/sessions/{sessionId}        # Session detail with spans
GET  /api/v1/sessions/{sessionId}/spans  # Spans for session
GET  /api/v1/traces/{traceId}            # Single trace
POST /api/v1/query                       # Advanced query DSL
```

### Realtime API
```
GET  /api/v1/live                        # SSE stream of all spans
GET  /api/v1/live?session={id}           # SSE stream filtered by session
```

### Ingestion API
```
POST /api/v1/ingest                      # qyl. native protocol (PRIMARY)
POST /v1/traces                          # OTLP shim (compatibility only)
```

### Feedback API
```
POST /api/v1/feedback                    # Submit feedback
GET  /api/v1/sessions/{id}/feedback      # Get feedback for session
```

---

## The Stack (Nachtmann Stack)

| Layer | Technology | Why |
|-------|-----------|-----|
| **Backend** | C# Native AOT | You speak it, ~25MB binary |
| **Storage** | DuckDB | Embedded columnar, no infra, 100M+ spans |
| **Cold Storage** | Parquet | Query S3 directly with DuckDB |
| **Frontend** | SolidJS | React-like, 7KB, actually fast |
| **State** | TanStack Query | Caching, virtual scrolling |
| **Codegen** | Scriban + YamlDotNet | Schema → SDK |

---

## What Doesn't Exist in qyl.

- ❌ Direct app → dashboard communication
- ❌ Multiple collectors
- ❌ External databases (Postgres, ClickHouse)
- ❌ OTLP as primary protocol
- ❌ OpenTelemetry Collector dependency
- ❌ Aspire
- ❌ Reflection-heavy OTel SDKs
- ❌ Alternatives or "you could also..."

---

## Migration from Current State

| Current | Becomes |
|---------|---------|
| `OpenTelemetry.Instrumentation.AI` | `qyl.collector` |
| `qyl.demo` models | Move to `qyl.sdk` |
| `qyl.analyzers` | Keep, validates qyl. attributes |
| `qyl.mcp.server` | Merge into `qyl.collector/Mcp` |

---

## Implementation Order

1. **qyl.schema** - Define YAML, build codegen
2. **qyl.sdk** - Fluent API + native exporter
3. **qyl.collector** - Ingest + DuckDB + Query API
4. **qyl.dashboard** - SolidJS consuming collector
5. **qyl.analyzers** - Update for qyl. attributes
6. **Polish** - Native AOT, Docker, docs

---

This is qyl.
Ship it.
