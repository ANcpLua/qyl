# Query Slice

MCP tooling, analytics API, and AG-UI protocol for telemetry access.

## Domain Objects

| Object    | Description                                  | src/ Mapping                       |
|-----------|----------------------------------------------|------------------------------------|
| MCP       | 60+ tools in 8 skill categories              | `qyl.mcp/`                        |
| Analytics | AI chat analytics API (6 modules)            | `qyl.collector/Analytics/`         |
| AG-UI     | Server-Sent Events protocol for CopilotKit   | `qyl.copilot/` (AG-UI endpoints)  |
| API       | REST endpoints for telemetry queries          | `qyl.collector/` (API endpoints)  |

## Scope

- MCP server (stdio + remote SSE) with 60+ tools across 8 `QylSkillKind` categories
- Streamable HTTP transport (roadmap)
- OAuth RFC 9728 for remote MCP (roadmap)
- AG-UI SSE protocol for frontend streaming
- NL → query translation (AssistedQueryTools)
- REST API for traces, logs, metrics, issues

## Cross-Slice Dependencies

- **ingestion/** provides the DuckDB tables that tools query
- **intelligence/** provides autofix/triage/review tools exposed via MCP
- **presentation/** consumes AG-UI SSE streams for real-time UI

## Key Files

```text
src/qyl.mcp/QylMcpServer.cs
src/qyl.mcp/Tools/*.cs
src/qyl.copilot/CopilotAguiEndpoints.cs
src/qyl.copilot/DeclarativeEngine.cs
src/qyl.collector/Analytics/AnalyticsEndpoints.cs
```

## Reference

- [loom-design.md §15.3](../roadmap/loom-design.md#153-mcp-platform-design) — MCP Platform Design
- [loom-design.md §15.5](../roadmap/loom-design.md#155-ag-ui--declarative-workflows) — AG-UI + Declarative Workflows
- [loom-design.md §17](../roadmap/loom-design.md#17-sentry-mcp-architecture) — sentry-mcp Reference Architecture
