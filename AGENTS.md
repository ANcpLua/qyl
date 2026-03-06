# qyl — AI Observability Platform

OTLP-native observability: ingest traces/logs/metrics, store in DuckDB, and expose query + AI workflows through REST, MCP, and AG-UI.

## Quick reference

- Stack: .NET 10.0 / C# 14 + React 19 / Vite 7 + Tailwind CSS 4 + DuckDB + OTel 1.40.
- Primary entry docs: [CLAUDE.md](CLAUDE.md), [README.md](README.md), [.github/copilot-instructions.md](.github/copilot-instructions.md).
- Ports: 5100 (HTTP), 4317 (gRPC OTLP), 4318 (HTTP OTLP), 5173 (dashboard dev).

## Core rules

- Follow established patterns and conventions before changing architecture.
- Avoid editing generated code directly (`*.g.cs`, generated API clients/types, migration artifacts).
- Do not bypass dependency constraints; update `docs/agent-guidance/architecture.md` first if uncertain.
- If something non-obvious appears, update this file or document it in `docs/agent-guidance/requests-to-humans.md`.
- Never edit `qyl.protocol` with external package dependencies (it is BCL-only by design).
- Use NuGet-Central Package Management for package versions (`Directory.Packages.props` + `Version.props`).

## Detailed guidance

- [Architecture and dependency rules](docs/agent-guidance/architecture.md)
- [Build/tooling workflow](docs/agent-guidance/build-and-tooling.md)
- [Coding conventions and constraints](docs/agent-guidance/conventions.md)
- [Docs and project map](docs/agent-guidance/docs-catalog.md)
- [Open requests and blockers](docs/agent-guidance/requests-to-humans.md)
