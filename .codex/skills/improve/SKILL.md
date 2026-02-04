---
name: improve
description: |
  Deep codebase analysis for library adoption, cohesive refactoring, and improvement opportunities
---

## Source Metadata

```yaml
plugin:
  name: "codebase-improver"
  version: "1.0.0"
  description: "Deep codebase analysis for library adoption, cohesive refactoring, and senior-level improvement suggestions. Knows when 'clean enough' is the right answer."
  author:
    name: "ANcpLua"
```


# /improve Command

Perform a comprehensive codebase analysis looking for improvement opportunities.

## What to do

1. **Spawn the codebase-improver agent** using the Task tool with `subagent_type: "codebase-improver"`

2. **Provide context** in the prompt:
   - Current working directory
   - Any specific focus areas mentioned by the user
   - Whether to check specific libraries

3. **The agent will:**
   - Discover the project's ecosystem position
   - Deep-dive into available utility libraries
   - Search for improvable patterns
   - Return honest assessment (opportunities OR "already clean")

## Example invocation

```
Task(
  subagent_type: "codebase-improver",
  prompt: "Analyze the codebase at [path] for improvement opportunities.
           Focus on: library adoption, pattern consistency, cohesive refactoring.
           Available ecosystem: ANcpLua.Roslyn.Utilities, testing infrastructure.
           Report concrete opportunities or confirm the code is already clean."
)
```

## User arguments

If the user provides arguments after `/improve`, include them:
- `/improve src/` → Focus on src directory
- `/improve --check-guards` → Specifically check Guard clause usage
- `/improve --testing` → Focus on test infrastructure usage
- `/improve` → Full codebase analysis

## Output expectations

The agent returns one of:
1. **Concrete opportunities** with before/after code examples
2. **"Already clean" assessment** confirming good practices
3. **Mixed** - some opportunities, some areas already optimal

Never return vague "could be improved" without specifics.
