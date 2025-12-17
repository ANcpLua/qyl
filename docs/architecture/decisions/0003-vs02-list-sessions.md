# ADR-0003: VS-02 List Sessions

## Metadata

| Field      | Value            |
|------------|------------------|
| Status     | Draft            |
| Date       | 2025-12-16       |
| Slice      | VS-02            |
| Priority   | P0               |
| Depends On | ADR-0002 (VS-01) |
| Supersedes | -                |

## Context

Nach dem Span Ingestion (VS-01) müssen Sessions aggregiert und angezeigt werden. Eine Session ist eine Gruppierung von
Traces/Spans mit gemeinsamer `session_id`. Für GenAI-Anwendungen zeigt die Session-Ansicht Token-Usage,
Modell-Verteilung und Fehlerrate.

## Decision

Implementierung einer Session-Aggregation mit:

- SQL-basierte Aggregation in DuckDB (performant für OLAP)
- REST API für Session-Listen und Details
- MCP Tool für AI Agents
- Dashboard-Komponenten für Visualisierung

## Layers

### 1. TypeSpec (Contract)

```yaml
files:
  - core/specs/api/sessions.tsp       # Session endpoints
  - core/specs/models/sessions.tsp    # SessionSummary, SessionDetail
generates:
  - core/generated/openapi/openapi.yaml   # OpenAPI spec update
  - src/qyl.dashboard/src/types/generated/ # Kiota TypeScript client
```

**sessions.tsp Example:**

```typespec
@route("/api/v1/sessions")
namespace Sessions {
  @get op list(
    @query limit?: int32 = 50,
    @query offset?: int32 = 0,
    @query serviceName?: string
  ): SessionListResponse;

  @get @route("/{sessionId}")
  op get(@path sessionId: string): SessionSummary | NotFoundResponse;
}

model SessionSummary {
  sessionId: string;
  serviceName: string;
  startTime: utcDateTime;
  endTime: utcDateTime;
  traceCount: int32;
  spanCount: int32;
  errorCount: int32;

  // GenAI specific
  totalInputTokens: int32;
  totalOutputTokens: int32;
  primaryModel?: string;
  estimatedCost?: float64;
}
```

### 2. Protocol Layer

```yaml
files:
  - src/qyl.protocol/Models/SessionSummary.cs
  - src/qyl.protocol/Contracts/ISessionAggregator.cs
```

### 3. Query Layer

```yaml
files:
  - src/qyl.collector/Query/SessionQueryService.cs
methods:
  - "GetSessionsAsync(limit, offset, serviceName)"
  - "GetSessionAsync(sessionId)"
  - "GetSessionStatsAsync(sessionId)"
```

**Aggregation SQL:**

```sql
SELECT
    session_id,
    service_name,
    MIN(start_time_unix_nano) as start_time,
    MAX(end_time_unix_nano) as end_time,
    COUNT(DISTINCT trace_id) as trace_count,
    COUNT(*) as span_count,
    SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END) as error_count,
    SUM(gen_ai_input_tokens) as total_input_tokens,
    SUM(gen_ai_output_tokens) as total_output_tokens,
    MODE(gen_ai_request_model) as primary_model
FROM spans
WHERE session_id IS NOT NULL
GROUP BY session_id, service_name
ORDER BY start_time DESC
LIMIT ?
OFFSET ?
```

### 4. API Layer

```yaml
endpoints:
  - "GET /api/v1/sessions?limit=50&offset=0&serviceName=myapp"
  - "GET /api/v1/sessions/{sessionId}"
  - "GET /api/v1/sessions/{sessionId}/spans"
files:
  - src/qyl.collector/Program.cs   # Endpoint registration
  - src/qyl.collector/Mapping/SessionMapper.cs  # DTO mapping
response:
  SessionListResponse:
    sessions: SessionSummary[]
    total: int32
    hasMore: boolean
```

### 5. MCP Layer

```yaml
files:
  - src/qyl.mcp/Tools/GetSessionTool.cs
  - src/qyl.mcp/Tools/ListSessionsTool.cs
tools:
  - name: "get_session"
    description: "Get details of a specific session"
    parameters:
      sessionId: string (required)
    returns: "SessionSummary as markdown table"

  - name: "list_sessions"
    description: "List recent sessions with GenAI stats"
    parameters:
      limit: int32 (default: 10)
      serviceName: string (optional)
    returns: "Markdown table of sessions"
```

### 6. Dashboard Layer

```yaml
files:
  - src/qyl.dashboard/src/api/hooks.ts           # useSessions(), useSession()
  - src/qyl.dashboard/src/components/sessions/SessionList.tsx
  - src/qyl.dashboard/src/components/sessions/SessionCard.tsx
  - src/qyl.dashboard/src/components/sessions/SessionDetail.tsx
  - src/qyl.dashboard/src/pages/SessionsPage.tsx
patterns:
  - "TanStack Query with 5s refetchInterval"
  - "SSE triggers invalidateQueries on new spans"
  - "Virtual scrolling for large lists"
```

## Acceptance Criteria

- [ ] TypeSpec `core/specs/api/sessions.tsp` compiliert
- [ ] `GET /api/v1/sessions` gibt SessionListResponse zurück
- [ ] `GET /api/v1/sessions/{id}` gibt SessionSummary oder 404 zurück
- [ ] GenAI Stats (Tokens, Model) werden korrekt aggregiert
- [ ] MCP `get_session` Tool funktioniert
- [ ] MCP `list_sessions` Tool funktioniert
- [ ] Dashboard zeigt Session-Liste mit Pagination
- [ ] Dashboard zeigt Session-Details mit GenAI Stats
- [ ] Auto-Refresh bei neuen Spans (via SSE in VS-05)
- [ ] Unit Tests für SessionQueryService
- [ ] Integration Tests für Session Endpoints

## Test Files

```yaml
unit_tests:
  - tests/qyl.collector.tests/Query/SessionQueryServiceTests.cs
  - tests/qyl.collector.tests/Mapping/SessionMapperTests.cs
integration_tests:
  - tests/qyl.collector.tests/Api/SessionEndpointsTests.cs
  - tests/qyl.mcp.tests/Tools/GetSessionToolTests.cs
```

## Consequences

### Positive

- **Übersicht**: User sieht alle Sessions auf einen Blick
- **GenAI Insights**: Token-Usage und Kosten sofort sichtbar
- **Agent-freundlich**: MCP Tools ermöglichen AI-Abfragen

### Negative

- **Aggregation-Kosten**: SQL Aggregation bei vielen Spans langsam
- **Refresh-Delay**: Polling alle 5s statt Echtzeit (Echtzeit in VS-05)

### Risks

| Risk                   | Impact | Likelihood | Mitigation                             |
|------------------------|--------|------------|----------------------------------------|
| Langsame Aggregation   | Medium | Medium     | Materialized View oder Pre-Aggregation |
| session_id fehlt       | Low    | High       | NULL-Sessions in "Ungrouped" zeigen    |
| Pagination Performance | Medium | Low        | Cursor-based pagination statt offset   |

## References

- [ADR-0002](0002-vs01-span-ingestion.md) - VS-01 Span Ingestion (Dependency)
- [TanStack Query](https://tanstack.com/query/latest) - Data Fetching
- [qyl-architecture.yaml](../../qyl-architecture.yaml) Section: projects.qyl.collector.Query
