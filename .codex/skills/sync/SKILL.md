---
name: sync
description: |
  Force sync even if docs are fresh (optional)
---

## Source Metadata

```yaml
frontmatter:
  arguments:
    - name: force
      required: false
  allowed-tools: Bash, Read, Write, Grep, Glob, WebFetch
plugin:
  name: "otelwiki"
  version: "1.0.6"
  description: "Unified OpenTelemetry documentation with auto-sync. Auto-triggers on telemetry work, provides semantic conventions, collector config, and .NET 10 instrumentation guidance."
  author:
    name: "AncpLua"
```


# OTel Documentation Sync

Sync the bundled OpenTelemetry documentation from upstream repositories.

## What This Does

1. Pulls latest from upstream OTel repos:
   - `opentelemetry-specification`
   - `opentelemetry-collector`
   - `opentelemetry.io`

2. Extracts .NET 10 relevant content only

3. Validates for:
   - No deprecated attributes
   - Correct attribute naming
   - OTLP-only examples

4. Updates `docs/VERSION.md` and `docs/SYNC-REPORT.md`

## Invocation

Spawn the otel-librarian agent to perform the sync:

```
Task(
  subagent_type="otelwiki:otel-librarian",
  prompt="Sync OTel docs from upstream repositories. Force sync: $ARGUMENTS"
)
```

## Arguments

- `force` - If provided, sync even if VERSION.md shows docs are fresh

## Examples

```
/otelwiki:sync           # Normal sync (skips if fresh)
/otelwiki:sync force     # Force sync regardless of freshness
```

## After Sync

The otel-expert skill will automatically use the updated docs. Check `docs/SYNC-REPORT.md` for details on what changed.
