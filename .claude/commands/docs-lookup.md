---
name: docs-lookup
description: Look up OTel conventions and ANcpLua SDK docs in parallel
arguments:
  - name: query
    description: "What to look up"
    required: true
---

# Docs Lookup

Launch 2 specialist agents to find documentation:

### Agent 1: OTel Conventions
```yaml
subagent_type: otel-librarian
prompt: |
  QUERY: {{ query }}

  Search OpenTelemetry documentation for:
  1. Semantic conventions (especially gen_ai.*)
  2. OTLP specifications
  3. Span/Trace structures
  4. Resource attributes
  5. Best practices

  Context: qyl is building an AI observability platform
  using OTel gen_ai.* semantic conventions v1.39.0

  Provide:
  - Relevant convention details
  - Attribute names and types
  - Example usage
  - Any gotchas
```

### Agent 2: ANcpLua SDK Docs
```yaml
subagent_type: ancplua-librarian
prompt: |
  QUERY: {{ query }}

  Search ANcpLua ecosystem documentation for:
  1. ANcpLua.NET.Sdk features
  2. Analyzer rules and fixes
  3. Roslyn utilities
  4. DSL patterns
  5. Best practices

  Context: qyl uses ANcpLua.NET.Sdk@1.1.8

  Provide:
  - Relevant SDK features
  - Analyzer warnings to watch for
  - Code patterns
  - Configuration options
```

---

## Quick Lookups

Common queries:
- `gen_ai.usage` - Token usage attributes
- `gen_ai.request.model` - Model specification
- `gen_ai.response` - Response attributes
- `sdk analyzers` - ANcpLua analyzer rules
- `roslyn utilities` - Code generation helpers
- `dsl patterns` - DSL implementation patterns

## Output

```markdown
# Docs Lookup: {{ query }}

## OTel Conventions
[Findings from otel-librarian]

## ANcpLua SDK
[Findings from ancplua-librarian]

## Applicable to qyl
[How this applies to current implementation]
```
