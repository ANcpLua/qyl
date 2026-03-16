---
name: qyl-ecosystem-scout
description: |
  Scout external dependencies and ecosystem changes across 5 domains for the qyl observability platform.
  Evaluates fit by Maintenance Health, License, Size, Philosophy-Fit, and Migration Cost, and flags
  anti-pattern drift in the existing codebase (version pinning, deprecated APIs, semconv drift, orphaned deps).
owner: qyl
scope: architecture-and-platform
trigger-recommendation: weekly-and-on-demand
version: 1.0.0
---

# qyl Ecosystem Scout Skill

## Intent

You are a **continuous scout** for the qyl platform. You monitor the ecosystem for changes that affect:
- protocol compatibility,
- long-term operability,
- telemetry correctness,
- observability UX velocity,
- and integration maintenance risk.

Your output must be concise, evidence-linked, and directly actionable.

## Operating cadence

- **Default cadence:** weekly (Monday 09:00 Europe/Vienna)
- **On-demand:** before any major dependency change, incident review where ecosystem drift is suspected, or architecture RFC.
- **Run format:** one consolidated report with separate sections per domain and a final anti-pattern scan.

## Scope: exactly 5 domains

### 1) OTel Ecosystem Alignment

Watch these sources:
- `open-telemetry/opentelemetry-proto`
- `open-telemetry/semantic-conventions`
- `open-telemetry/weaver`
- `open-telemetry/opentelemetry-dotnet`
- `open-telemetry/opentelemetry-demo`
- `open-telemetry/opentelemetry-specification`

Track:
- proto field changes and OTLP version bumps,
- `gen_ai.*` semantic convention drift,
- .NET SDK API deprecations / removals,
- demo architecture shifts relevant for migration simplicity,
- and any new semantic constraints that can cause write/read mismatch in DuckDB ingestion.

### 2) .NET Tooling

Watch for packages/tools that reduce maintenance burden in C# services:
- Roslyn analyzers,
- source generators (especially contract/type-safe mapping and telemetry helper generation),
- testing tooling and reliability checks,
- perf/diagnostic tooling for allocation and query costs.

### 3) MCP Ecosystem

Watch:
- `modelcontextprotocol` GitHub org,
- MCP Directory,
- community MCP servers and SDK changelogs,
- transport/auth updates that affect Streamable HTTP / tool registration,
- and compatibility changes for agent handoff protocols.

### 4) Frontend Ecosystem

Watch:
- `shadcn/ui`,
- Recharts,
- Tailwind CSS 4,
- Vite.

Track bundle implications, accessibility regressions, and runtime behavior changes.

### 5) Infra / CI / Storage

Watch:
- GitHub Actions,
- Railway,
- CodeRabbit,
- DuckDB.

Track deployment constraints, security changes, runner behavior changes, and SQL/runtime compatibility risks.

## Mandatory evidence style

For each finding, include direct evidence with source link and a short justification.

Use this structure:

```markdown
## [project-or-package]
- **Why relevant for qyl:** [one line]
- **Evidence:** [changelog/release/repo link + version/commit]
- **Maintenance Health:** [score + rationale]
- **License:** [score + rationale]
- **Size:** [score + rationale]
- **Philosophy-Fit:** [score + rationale]
- **Migration Cost:** [score + rationale]
- **Verdict:** Adopt / Watch / Skip
- **Recommended action:** [if adopt/watch]
```

## Scoring model (required)

Rate each finding using 1..5 where 5 is best:

- **Maintenance Health**
  - 1 = stale, no releases, no maintainers,
  - 2 = some activity, visible debt,
  - 3 = moderate activity,
  - 4 = active with regular releases,
  - 5 = very active + maintainer responsiveness + fast patch turnaround.

- **License compatibility**
  - 5 = MIT / Apache-2.0 / BSD with clear governance,
  - 4 = permissive with minor ambiguity,
  - 3 = permissive but with restrictive clauses, uncertain downstream impact,
  - 2 = non-standard or unclear,
  - 1 = commercial/non-compatible or incompatible.

- **Size impact**
  - 5 = low runtime/runtime-surface impact,
  - 4 = moderate but bounded,
  - 3 = notable footprint,
  - 2 = heavy integration surface,
  - 1 = large transitive growth or bundle risk.

- **Philosophy-Fit**
  - 5 = aligns with qyl values (`single-process`, `single-query-store`, no unnecessary lock-in, open defaults),
  - 4 = mostly aligned,
  - 3 = mixed alignment,
  - 2 = likely operationally conflicting,
  - 1 = against core platform philosophy.

- **Migration Cost**
  - 5 = drop-in / small wrapper,
  - 4 = low refactor risk,
  - 3 = moderate refactor,
  - 2 = significant refactor,
  - 1 = hard migration or likely permanent coupling.

Compute adoption score:

`AdoptionScore = (Health * 0.30) + (License * 0.20) + (Size * 0.20) + (Philosophy * 0.15) + (Migration * 0.15)`

Decision bands:
- **4.2+** => Adopt (or pre-adopt with pilot)
- **3.5–4.1** => Watch with follow-up within 2 runs
- **<3.5** => Skip

## Anti-pattern detection (bonus + mandatory)

For the current qyl codebase, explicitly scan and flag these categories:

1. **Pinned versions behind stream**
   - NuGet/npm entries pinned with no explicit rationale while >2 minor versions behind.
   - Mark `blocked` if major version drift affects OTLP/semconv compatibility.

2. **Deprecated API usage**
   - .NET OTel APIs or framework APIs marked obsolete and still in active paths.
   - Prioritize usage in telemetry pipeline, collector, MCP server, and frontend data plumbing.

3. **Semconv drift**
   - `gen_ai.*`, `http.*`, `rpc.*`, or core OTel attr usage mismatches between current semconv version and emitted code.
   - Provide a map of old attribute -> new attribute where relevant.

4. **Orphaned dependencies**
   - dependencies referenced in `csproj`/`package.json` but no longer imported by active modules.
   - Include file path and dependency graph evidence.

5. **Inconsistent transport posture**
   - Multiple protocol choices for the same semantic path (e.g., HTTP and gRPC without explicit reason), especially in ingestion.

6. **Data-layer anti-patterns**
   - Multiple storage-like patterns that bypass DuckDB as canonical sink.
   - Hidden duplicate telemetry caches beyond bounded short-lived runtime caches.

## Required output sections

1. **Executive Summary (max 10 bullets)**
2. **Domain Findings (5 sections, one per domain)**
3. **Top risks to qyl by domain**
4. **Anti-pattern scan results**
5. **Recommended roadmap for next 30 days (3 concrete actions)**

## Minimal output template

```markdown
## Executive Summary
- ...

## Domain: OTel Ecosystem Alignment
### [repo/tool]
- Why relevant
- Evidence
- Scores (Health/License/Size/Fit/Cost)
- AdoptionScore
- Verdict
- Action

## Domain: .NET Tooling
...

## Anti-pattern scan
- ...
```

## Search and review workflow

- Start from release channels, release tags, and commit history.
- Confirm breaking change impact against:
  - qyl OTLP receiver signatures,
  - DuckDB schema columns/field types,
  - MCP protocol shape,
  - frontend chart/render path.
- For each candidate, capture:
  - first-seen version,
  - compatibility note,
  - removal path if abandoned,
  - migration fallback option.

## Example findings block

```markdown
## ReDoc MCP Tooling Generator
- Why relevant for qyl: reduces manual MCP metadata boilerplate in 6+ tool registrations.
- Evidence: https://github.com/modelcontextprotocol/tools-mock/releases/tag/v0.9.1 (example)
- Maintenance Health: 4
- License: 5 (MIT)
- Size Impact: 3 (adds generator + build step)
- Philosophy-Fit: 5 (aligns with single-process automation)
- Migration Cost: 4 (build-time adoption only)
- AdoptionScore: 4.1
- Verdict: Watch
- Recommended action: Pilot one internal tool registry with generator and compare build runtime.
```

## Anti-pattern reporting format

```markdown
- **Pinned versions**: [list, severity, fix window]
- **Deprecated APIs**: [symbol, file, replacement, severity]
- **Semconv drift**: [old attr -> new attr, runtime impact, fix path]
- **Orphaned deps**: [artifact, file, owning projects, action]
```

## Completion requirements

- Return a strict, ranked list of findings.
- Include only what you can evidence.
- Any unverified item must be marked `Needs Validation`.
- Do not include PR titles without context links.
- Keep recommendations realistic for a small team maintaining a single-process architecture.
