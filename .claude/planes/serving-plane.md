# Serving plane

Mission:
- expose stable platform state to operators, MCP clients, and internal services

Owns:
- REST endpoints
- SSE streams
- MCP-facing read/write adapters
- pagination, filters, and response envelopes
- query/materialization boundaries

Depends on:
- data plane
- intelligence plane
- ledger/governance plane

Must not depend on:
- free-form agent reasoning
- UI component decisions

Current qyl areas:
- collector endpoint registration
- MCP HTTP/client surfaces
- `specs/api.md`
- `specs/mcp.md`

Success condition:
- platform capabilities are reachable through explicit contracts without requiring an agent in the request path.
