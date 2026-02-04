---
name: parallel-explore
description: |
  Comma-separated questions
---

## Source Metadata

```yaml
frontmatter:
  arguments:
    - name: questions
      required: true
```


# Parallel Explore

Split questions across parallel agents (Haiku for speed):

{{ questions }}

Launch one Explore agent per question:

```yaml
subagent_type: Explore
model: haiku
prompt: |
  QUESTION: [individual question]

  Explore qyl codebase to answer.
  Reference: core/, docs/, CLAUDE.md

  Provide:
  1. Direct answer
  2. File:line references
  3. Code snippets
```

---

## Common Question Sets

**Architecture:**
```
/parallel-explore questions="How does OTLP ingestion work?, Where are session aggregations?, How does MCP communicate with collector?"
```

**OTel:**
```
/parallel-explore questions="Which gen_ai attributes are used?, How are spans parsed?, Where is trace context handled?"
```

**Types:**
```
/parallel-explore questions="Where is SessionId defined?, What interfaces exist in protocol?, How are records serialized?"
```
