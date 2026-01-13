---
name: slice-validate
description: Validate vertical slice completeness end-to-end
arguments:
  - name: slice
    description: "Slice to validate: VS-01|VS-02|VS-03|VS-04|all"
    default: "all"
---

# Vertical Slice Validation

Each slice must be complete: TypeSpec → Storage → Query → API → MCP → Dashboard

## Slice: {{ slice }}

{{#if (eq slice "all")}}
Launch 4 agents parallel, one per slice:

### Agent VS-01: Span Ingestion
```yaml
subagent_type: feature-dev:code-explorer
prompt: |
  SLICE: VS-01 Span Ingestion (P0, PARTIAL)

  TRACE COMPLETE PATH:
  1. TypeSpec: Is SpanRecord defined in protocol?
  2. Storage: Is DuckDB table spans created?
  3. Query: Can spans be retrieved?
  4. API: POST /traces endpoint working?
  5. MCP: Any tools using spans?
  6. Dashboard: Any span visualization?

  CHECK:
  - OTLP JSON parsing (OtlpJsonSpanParser)
  - gen_ai.* attribute extraction
  - Batch ingestion performance

  Output: VS-01 completeness checklist
```

### Agent VS-02: List Sessions
```yaml
subagent_type: feature-dev:code-explorer
prompt: |
  SLICE: VS-02 List Sessions (P0, IN_PROGRESS)

  TRACE COMPLETE PATH:
  1. TypeSpec: SessionSummary in protocol?
  2. Storage: Session aggregation query?
  3. Query: ISessionAggregator implementation?
  4. API: GET /sessions endpoint?
  5. MCP: list_sessions tool?
  6. Dashboard: Session list component?

  CHECK:
  - Aggregation logic correct
  - Pagination support
  - SSE updates for new sessions

  Output: VS-02 completeness checklist
```

### Agent VS-03: View Trace Tree
```yaml
subagent_type: feature-dev:code-explorer
prompt: |
  SLICE: VS-03 View Trace Tree (P1, NOT_STARTED)

  TRACE COMPLETE PATH:
  1. TypeSpec: TraceNode in protocol?
  2. Storage: Parent-child span query?
  3. Query: Tree building logic?
  4. API: GET /traces/{traceId}/tree endpoint?
  5. MCP: view_trace tool?
  6. Dashboard: Tree visualization component?

  CHECK:
  - TraceId → Spans → Tree construction
  - Nested span handling
  - UI tree rendering

  Output: VS-03 completeness checklist
```

### Agent VS-04: GenAI Analytics
```yaml
subagent_type: feature-dev:code-explorer
prompt: |
  SLICE: VS-04 GenAI Analytics (P1, NOT_STARTED)

  TRACE COMPLETE PATH:
  1. TypeSpec: GenAiSpanData, GenAiAttributes in protocol?
  2. Storage: GenAI-specific queries?
  3. Query: Token counts, model usage aggregation?
  4. API: Analytics endpoints?
  5. MCP: analytics tools?
  6. Dashboard: Charts/metrics components?

  CHECK:
  - gen_ai.usage.* token tracking
  - gen_ai.request/response.model
  - Cost calculation

  Output: VS-04 completeness checklist
```
{{else}}
Launch single agent for {{ slice }}:

### Agent: {{ slice }} Validator
```yaml
subagent_type: feature-dev:code-explorer
prompt: |
  Validate slice {{ slice }} end-to-end.
  Check all 6 layers: TypeSpec → Storage → Query → API → MCP → Dashboard
  Report missing/incomplete layers.
```
{{/if}}

---

## Output

```markdown
# Slice Validation Report

| Slice | TypeSpec | Storage | Query | API | MCP | Dashboard | Status |
|-------|----------|---------|-------|-----|-----|-----------|--------|
| VS-01 | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ | PARTIAL |
| VS-02 | ... | ... | ... | ... | ... | ... | IN_PROGRESS |
| VS-03 | ... | ... | ... | ... | ... | ... | NOT_STARTED |
| VS-04 | ... | ... | ... | ... | ... | ... | NOT_STARTED |

## Missing Implementations
[List of specific gaps per slice]

## Recommended Next Steps
1. [Priority action items]
```
