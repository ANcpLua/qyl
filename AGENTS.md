# qyl — AI Observability Platform

@Version.props

OTLP-native observability: ingest traces/logs/metrics, store in DuckDB, query via API/MCP/Copilot.
Single Docker image. Single process.

Specs: `specs/SUMMARY.md`
Decisions: `specs/decisions/`

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
