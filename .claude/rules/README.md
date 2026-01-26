# Claude Code Rules

Modular, topic-specific guidelines for the qyl project. These are cross-cutting concerns that apply across multiple components.

## Rules Organization

| Rule | Scope | Purpose |
|------|-------|---------|
| `architecture-rules.md` | Project structure | Type ownership, dependencies, SSOT |
| `coding-patterns.md` | C# code | .NET 10, banned APIs, locking patterns |
| `genai-semconv.md` | Telemetry | OTel 1.39 GenAI semantic conventions |
| `build-workflow.md` | Build system | NUKE targets, codegen, Docker |
| `codegen.md` | Generated files | Never edit *.g.cs, always edit TypeSpec |
| `frontend.md` | React/TS | Dashboard development guidelines |

## Path-Specific Rules

Rules with `paths:` frontmatter only apply to matching files. Rules without `paths:` apply globally.

**Example:**
```markdown
---
paths:
  - "src/**/*.cs"
---

# Your Rule Title
```

## Long-Lived Knowledge Only

These rules should contain **stable, long-lived information**:
- ✅ Architecture principles (type ownership, dependencies)
- ✅ Semantic conventions (OTel 1.39 GenAI attributes)
- ✅ Build workflows (NUKE target graph)
- ✅ Coding patterns (locking, JSON, time)

**Do NOT include:**
- ❌ Package versions (use Version.props, Directory.Packages.props)
- ❌ Temporary workarounds
- ❌ Project-specific implementation details (use component CLAUDE.md)

## Adding New Rules

Create focused, single-topic `.md` files in this directory. Use YAML frontmatter for path-specific rules.

## Import System

Root CLAUDE.md imports these rules via `@.claude/rules/*.md` syntax. This keeps the main file clean while maintaining access to all guidelines.
