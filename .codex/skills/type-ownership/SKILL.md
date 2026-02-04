---
name: type-ownership
description: |
  Validate type ownership rules - shared types in protocol, internal types in owning project
---

## Source Metadata

```yaml
# none
```


# Type Ownership Validator

Launch 3 agents parallel to scan each project:

### Agent 1: Collector Types
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.collector/**/*.cs

  MISSION: Find types that should be in protocol.

  RULE: If a type in collector is used by mcp or dashboard → VIOLATION

  SCAN FOR:
  1. Public types that could be shared
  2. Types duplicated from protocol
  3. DTOs that mcp might need
  4. Types referenced in API responses

  KNOWN INTERNAL (OK):
  - DuckDbStore
  - DuckDbSchema
  - SpanStorageRow
  - OtlpJsonSpanParser
  - OtlpTypes

  FIND ANY public type not in this list that might be needed elsewhere.

  Output: List of potential ownership violations
```

### Agent 2: MCP Types
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  TERRITORY: core/qyl.mcp/**/*.cs

  MISSION: Find types that should be in protocol.

  RULE: If a type in mcp is used by collector or dashboard → VIOLATION

  SCAN FOR:
  1. Request/Response DTOs that match collector APIs
  2. Types duplicated from protocol
  3. Any shared data structures

  KNOWN INTERNAL (OK):
  - Tool implementations
  - MCP-specific configs

  Output: List of potential ownership violations
```

### Agent 3: Protocol Completeness
```yaml
subagent_type: feature-dev:code-explorer
prompt: |
  TERRITORY: core/qyl.protocol/**/*.cs

  MISSION: Verify protocol has all shared types.

  EXPECTED TYPES:
  - SessionId, UnixNano, TraceId, SpanId (Primitives)
  - SpanRecord, SessionSummary, TraceNode (Records)
  - GenAiSpanData, GenAiAttributes (Attributes)
  - ISpanStore, ISessionAggregator (Interfaces)

  CHECK:
  1. All expected types exist
  2. No implementation details leaked
  3. Types are records/interfaces only (no classes with logic)
  4. Correct namespaces

  Output: Protocol type inventory with status
```

---

## Aggregation

```markdown
# Type Ownership Report

## Golden Rule Compliance
> Types used by >1 project MUST be in protocol
> Types used by 1 project MUST be in that project

## Violations Found

| Type | Current Location | Should Be | Reason |
|------|------------------|-----------|--------|
| ... | collector | protocol | Used by mcp |

## Protocol Inventory

| Type | Status | Consumers |
|------|--------|-----------|
| SessionId | ✅ | collector, mcp |
| ... | ... | ... |

## Duplicate Types
[Any types defined in multiple places]

## Recommendations
1. Move X to protocol
2. Delete duplicate Y
```
