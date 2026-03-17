# qyl

- [Architecture](./00-architecture.md) — product identity, deployment, component boundaries, dependency rules, kill list, cost engine

# Subsystem Specs

- [Collector](./collector.md) — OTLP ingestion, DuckDB storage, SSE streaming, REST API, auth
- [Contracts](./contracts.md) — TypeSpec-generated shared types, BCL-only
- [Instrumentation](./instrumentation.md) — 3-layer build model, Roslyn generators, runtime OTel wiring
- [Loom](./loom.md) — AI investigation, 5-stage autofix pipeline, regression, triage, code review
- [MCP Server](./mcp.md) — MCP tool surface, skills/auth, deployment modes, response format
- [Dashboard](./dashboard.md) — React telemetry UI, operator-grade density, charts, primitives

# Agent Intelligence Specs

- [Telemetry Data Model](./telemetry-data-model.md) — canonical schema, promoted columns, attribute inventory
- [Issue Fingerprinting](./issue-fingerprinting.md) — grouping algorithm, stacktrace normalization, issue lifecycle

# Decisions

- [No Proxy Gateway](./decisions/no-proxy.md)
- [No Helicone Sidecar](./decisions/no-helicone.md)
- [Loom as Standalone Product](./decisions/loom-standalone.md)
- [MAF Native Migration](./decisions/maf-native-migration.md)
