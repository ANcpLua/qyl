---
name: swarm-audit
description: |
  Audit mode: full|protocol|collector|mcp|dashboard|otel|tournament
---

## Source Metadata

```yaml
frontmatter:
  arguments:
    - name: mode
      default: "full"
```


# qyl Swarm Audit

Launch ALL agents in a SINGLE message for true parallelism.

## Mode: {{ mode }}

{{#if (eq mode "full")}}
## FULL AUDIT - 8 Agents Parallel

### Agent 1: Protocol Type Ownership
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.protocol/**

  MISSION: Validate type ownership rules from CLAUDE.md.

  GOLDEN RULE CHECK:
  - Types used by >1 project MUST be in qyl.protocol
  - Types used by 1 project MUST be in that project

  FIND VIOLATIONS:
  1. Types in collector that should be in protocol
  2. Types in mcp that should be in protocol
  3. Duplicate types across projects
  4. Missing interfaces in protocol

  Output: Type ownership violation report with file:line
```

### Agent 2: Vertical Slice Completeness
```yaml
subagent_type: feature-dev:code-explorer
prompt: |
  TERRITORY: Entire repo

  MISSION: Audit vertical slices from CLAUDE.md.

  FOR EACH SLICE (VS-01 to VS-04):
  1. TypeSpec → Is schema defined?
  2. Storage → Is DuckDB table created?
  3. Query → Is data retrieval working?
  4. API → Are endpoints exposed?
  5. MCP → Are tools registered?
  6. Dashboard → Is UI rendering?

  FIND:
  - Incomplete slices
  - Missing layers
  - Orphaned code not in any slice

  Output: Slice completeness matrix
```

### Agent 3: OTel Semantic Conventions
```yaml
subagent_type: otel-librarian
prompt: |
  TERRITORY: core/qyl.protocol/**, core/qyl.collector/**

  MISSION: Validate OpenTelemetry semantic conventions compliance.

  CHECK:
  1. gen_ai.* attribute names match v1.39.0 spec
  2. Span naming follows OTel conventions
  3. Resource attributes correct
  4. No custom attributes where standard exists
  5. TraceId/SpanId format compliance

  REFERENCE: OTel Semantic Conventions documentation

  Output: OTel compliance report
```

### Agent 4: ANcpLua SDK Usage
```yaml
subagent_type: ancplua-librarian
prompt: |
  TERRITORY: **/*.csproj, **/*.cs

  MISSION: Validate ANcpLua.NET.Sdk@1.1.8 usage.

  CHECK:
  1. SDK version consistency across projects
  2. Correct SDK features being used
  3. Analyzer warnings addressed
  4. Roslyn utilities used correctly
  5. No deprecated API usage

  REFERENCE: ANcpLua SDK documentation

  Output: SDK compliance report
```

### Agent 5: Collector Storage Review
```yaml
subagent_type: deep-debugger
prompt: |
  TERRITORY: core/qyl.collector/**

  MISSION: Audit DuckDB storage implementation.

  CHECK:
  1. DuckDbStore correctness
  2. Schema migrations
  3. Query performance patterns
  4. Resource cleanup (IDisposable)
  5. Thread safety
  6. OTLP ingestion parsing

  FIND:
  - Potential data loss scenarios
  - Performance bottlenecks
  - Memory leaks

  Output: Storage health report
```

### Agent 6: MCP Server Review
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.mcp/**

  MISSION: Audit MCP server implementation.

  CHECK:
  1. HTTP communication with collector (no ProjectReference!)
  2. Tool registration completeness
  3. Error handling
  4. Session management
  5. Response formatting

  CRITICAL: MCP must use HTTP to collector, NOT direct reference

  Output: MCP server health report
```

### Agent 7: Dashboard Frontend Review
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.dashboard/**

  MISSION: Audit React 19 dashboard.

  CHECK:
  1. Generated types from QylSchema.cs
  2. TanStack Query patterns
  3. Tailwind 4 usage
  4. SSE handling for real-time
  5. Component structure
  6. Type safety

  FIND:
  - Stale generated types
  - Query cache issues
  - UI/UX problems

  Output: Frontend health report
```

### Agent 8: Build System & Dependencies
```yaml
subagent_type: dotnet-mtp-advisor
prompt: |
  TERRITORY: **/*.csproj, Directory.*.props, *.slnx

  MISSION: Validate build configuration.

  CHECK:
  1. DuckDB.NET.Data.Full@1.4.3 pinned correctly
  2. ModelContextProtocol package version
  3. CPM compliance
  4. TFM consistency (.NET 10)
  5. No invalid ProjectReferences (mcp → collector is BANNED)

  Run: dotnet build to verify

  Output: Build health report
```
{{/if}}

{{#if (eq mode "otel")}}
## OTEL FOCUS - 4 Agents

### Agent 1: Semantic Conventions
```yaml
subagent_type: otel-librarian
prompt: Verify gen_ai.* attributes match OTel v1.39.0 spec
```

### Agent 2: Span Structure
```yaml
subagent_type: feature-dev:code-explorer
prompt: Trace OTLP ingestion → SpanRecord → Storage flow
```

### Agent 3: Resource Attributes
```yaml
subagent_type: feature-dev:code-reviewer
prompt: Verify resource attributes (service.name, telemetry.sdk.*)
```

### Agent 4: Trace Context
```yaml
subagent_type: deep-debugger
prompt: Verify TraceId/SpanId/ParentSpanId propagation
```
{{/if}}

{{#if (eq mode "protocol")}}
## PROTOCOL FOCUS - 4 Agents

### Agent 1: Type Ownership
```yaml
subagent_type: feature-dev:code-reviewer
prompt: Verify all shared types are in protocol, no duplicates
```

### Agent 2: Interface Contracts
```yaml
subagent_type: feature-dev:code-architect
prompt: Review ISpanStore, ISessionAggregator interfaces
```

### Agent 3: Primitive Types
```yaml
subagent_type: feature-dev:code-reviewer
prompt: Verify SessionId, UnixNano, TraceId, SpanId implementations
```

### Agent 4: Record Structures
```yaml
subagent_type: feature-dev:code-reviewer
prompt: Review SpanRecord, SessionSummary, TraceNode records
```
{{/if}}

{{#if (eq mode "collector")}}
## COLLECTOR FOCUS - 3 Agents

### Agent 1: DuckDB Storage
```yaml
subagent_type: deep-debugger
prompt: |
  TERRITORY: core/qyl.collector/Storage/**

  Audit DuckDbStore implementation:
  - Schema correctness
  - Query performance
  - Resource disposal
  - Thread safety
```

### Agent 2: OTLP Ingestion
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.collector/Ingestion/**

  Audit OTLP parsing:
  - OtlpJsonSpanParser correctness
  - gen_ai.* attribute extraction
  - Batch processing
  - Error handling
```

### Agent 3: Session Aggregation
```yaml
subagent_type: feature-dev:code-explorer
prompt: |
  TERRITORY: core/qyl.collector/Aggregation/**

  Audit session aggregation:
  - ISessionAggregator implementation
  - Query correctness
  - Performance patterns
```
{{/if}}

{{#if (eq mode "mcp")}}
## MCP FOCUS - 3 Agents

### Agent 1: Tool Registration
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.mcp/Tools/**

  Audit MCP tools:
  - list_sessions tool
  - view_trace tool
  - Tool schemas correct
  - Error responses
```

### Agent 2: HTTP Communication
```yaml
subagent_type: deep-debugger
prompt: |
  TERRITORY: core/qyl.mcp/**

  CRITICAL CHECK: MCP uses HTTP to collector, NOT ProjectReference.

  Verify:
  - HttpClient usage correct
  - No direct collector references
  - Error handling
  - Retry patterns
```

### Agent 3: Response Formatting
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.mcp/**

  Audit response formatting:
  - JSON serialization
  - Error messages
  - Type consistency with protocol
```
{{/if}}

{{#if (eq mode "dashboard")}}
## DASHBOARD FOCUS - 3 Agents

### Agent 1: Generated Types
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.dashboard/src/types/**

  Verify TypeScript types match QylSchema.cs:
  - SessionSummary
  - SpanRecord
  - TraceNode
  - No stale types
```

### Agent 2: Data Fetching
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.dashboard/src/**

  Audit TanStack Query usage:
  - Query keys correct
  - Cache invalidation
  - SSE subscriptions
  - Error states
```

### Agent 3: Components
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.dashboard/src/components/**

  Audit React 19 components:
  - Tailwind 4 patterns
  - Accessibility
  - Loading states
  - Type safety
```
{{/if}}

{{#if (eq mode "tournament")}}
## TOURNAMENT - 4 Competitive Agents

Scoring: HIGH=3pts, MED=1pt, nitpick=-2pts

### Agent ALPHA: Architecture Hunter
```yaml
subagent_type: framework-migration:architect-review
prompt: |
  COMPETING for points. Find architecture violations:
  - Type ownership breaks
  - Layer boundary violations
  - Dependency direction errors
  - Missing slices
```

### Agent BETA: Bug Hunter
```yaml
subagent_type: deep-debugger
prompt: |
  COMPETING for points. Find bugs:
  - Null refs in collector
  - Resource leaks
  - Race conditions
  - DuckDB query errors
```

### Agent GAMMA: OTel Expert
```yaml
subagent_type: otel-librarian
prompt: |
  COMPETING for points. Find OTel issues:
  - Wrong attribute names
  - Missing conventions
  - Invalid span structures
```

### Agent DELTA: SDK Expert
```yaml
subagent_type: ancplua-librarian
prompt: |
  COMPETING for points. Find SDK issues:
  - Wrong SDK usage
  - Missing analyzer fixes
  - Deprecated APIs
```
{{/if}}

---

## Aggregation

After ALL agents complete, create `QYL-AUDIT-REPORT.md` with:
- Critical issues (blocking)
- High priority issues
- Type ownership violations
- OTel compliance status
- Slice completeness matrix
