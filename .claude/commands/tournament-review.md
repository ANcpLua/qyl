---
name: tournament-review
description: Competitive code review with 4 specialized agents
arguments:
  - name: target
    description: "Target: staged|collector|mcp|dashboard|protocol"
    default: "staged"
---

# Tournament Review

4 agents compete. Scoring: HIGH=3pts, MED=1pt, nitpick=-2pts

## Target: {{ target }}

### Agent ALPHA: Architecture Hunter
```yaml
subagent_type: framework-migration:architect-review
prompt: |
  COMPETING. Target: {{ target }}

  Find architecture issues:
  - Type ownership violations
  - Layer boundary breaches
  - Slice incompleteness
  - Dependency direction errors

  Every issue needs file:line proof.
  Nitpicks cost you points.
```

### Agent BETA: Bug Hunter
```yaml
subagent_type: deep-debugger
prompt: |
  COMPETING. Target: {{ target }}

  Find bugs:
  - Null refs in DuckDB operations
  - Resource leaks (IDisposable)
  - Race conditions in SSE
  - HTTP client misuse in mcp

  Crash bugs = 5pts. Prove with repro steps.
```

### Agent GAMMA: OTel Specialist
```yaml
subagent_type: otel-librarian
prompt: |
  COMPETING. Target: {{ target }}

  Find OTel issues:
  - Wrong gen_ai.* attribute names
  - Missing semantic conventions
  - Invalid span structures
  - OTLP parsing errors

  Reference v1.39.0 spec.
```

### Agent DELTA: Type Safety
```yaml
subagent_type: feature-dev:code-reviewer
prompt: |
  COMPETING. Target: {{ target }}

  Find type issues:
  - Nullable misuse
  - Missing validation
  - Incorrect record equality
  - JSON serialization problems

  Focus on runtime errors.
```

---

## Scoreboard

| Agent | Issues | Points |
|-------|--------|--------|
| ALPHA | | |
| BETA | | |
| GAMMA | | |
| DELTA | | |

Winner gets mentioned in commit message.
