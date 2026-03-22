# qyl — AI Observability Platform

@Version.props

OTLP-native observability: ingest traces/logs/metrics, store in DuckDB, query via API/MCP/Copilot.
Single Docker image. Single process.

<specs>

| Spec | What it covers |
|------|---------------|
| `specs/00-architecture.md` | Product identity, scope, component boundaries, dependency rules, kill list |
| `specs/api.md` | Response envelope, error model, status codes, pagination, timestamps, entity IDs |
| `specs/collector.md` | OTLP ingestion, DuckDB storage, SSE streaming, REST API, auth |
| `specs/contracts.md` | TypeSpec-generated shared types, BCL-only |
| `specs/cost.md` | Cost formula, pricing schema, aggregation endpoints, budget alerts |
| `specs/instrumentation.md` | 3-layer build model, Roslyn generators, runtime wiring, code.* emission (compile-time) |
| `specs/loom.md` | AI investigation, 5-stage autofix pipeline, regression, triage, code review |
| `specs/mcp.md` | MCP tool surface, skills/auth, deployment modes, tool contract |
| `specs/dashboard.md` | React telemetry UI, operator-grade density, charts, Base UI primitives |
| `specs/telemetry-data-model.md` | Canonical DuckDB schema: spans, logs, issues, deployments, sessions, promoted columns |
| `specs/issue-fingerprinting.md` | Error grouping algorithm, categorization, stacktrace normalization, issue lifecycle |
| `specs/telemetry-intelligence.md` | Canonical reasoning model: diagnostic patterns, causal rules, investigation strategies — TypeSpec → generated types |
| `specs/decisions/` | ADRs: no-proxy, no-helicone, loom-standalone, maf-native-migration |

</specs>

Spec ownership note:
- `specs/api.md` owns cross-cutting HTTP invariants only. Per-feature route inventories belong in the owning feature specs and should be verified from runtime endpoint metadata, not hand-maintained in `specs/api.md`.

```bash
nuke          # build
nuke test     # test
dotnet run --project src/qyl.collector   # run
```

<changelog-protocol priority="highest">

**CHANGELOG.md is the coordination layer. Multiple agents and humans work on this repo in parallel.**

Before starting work:
- Read `CHANGELOG.md` to check if your planned change already exists or conflicts with recent work.

Before finishing work:
- Update `CHANGELOG.md` with what you did (Added/Changed/Fixed/Removed under `## Unreleased`).
- Verify your entry doesn't duplicate existing entries.
- Do NOT commit or push until the changelog reflects your contribution.

This is not optional. The changelog is how parallel agents and the human owner stay in sync.
If your work isn't in the changelog, it didn't happen.

</changelog-protocol>

<ground-truth>

- .NET 10.0 LTS, C# 14, net10.0
- React 19, Vite 7, Tailwind CSS 4
- DuckDB (columnar, glibc required)
- OTel Semantic Conventions 1.40
- xUnit v3, Microsoft Testing Platform
- NUKE build system

Banned: `DateTime.Now`, `Newtonsoft.Json`, `object _lock`, `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`, `ISourceGenerator`, `SyntaxFactory.NormalizeWhitespace()`, runtime reflection, `dynamic`, `.Result`, `.Wait()`

</ground-truth>
