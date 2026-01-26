# Claude Code Rules

Modular, topic-specific guidelines for the qyl project.

## Rules Organization

| Rule | Scope | Purpose |
|------|-------|---------|
| `codegen.md` | Generated files | Never edit *.g.cs, always edit TypeSpec |
| `dotnet.md` | C# code | .NET 10, banned APIs, patterns |
| `otel.md` | Telemetry | OTel 1.39 semantic conventions |
| `frontend.md` | React/TS | Dashboard development guidelines |

## Path-Specific Rules

Rules with `paths:` frontmatter only apply to matching files. Rules without `paths:` apply globally.

## Adding New Rules

Create focused, single-topic `.md` files in this directory. Use YAML frontmatter for path-specific rules:

```markdown
---
paths:
  - "src/**/*.cs"
---

# Your Rule Title
```
