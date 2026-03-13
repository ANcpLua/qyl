---
paths:
  - "src/qyl.mcp/**"
---

# MCP Server Rules

## Context
- OAuth and connector flow overview: `.claude/qyl-workflows/README.md#section-1-architektur-ueberblick`

## Architecture
- Supports both `stdio` and streamable HTTP (`/mcp`) transports.
- Consumed by AI agents (Claude, Copilot, desktop tools, remote MCP connectors).
- Communicates with collector via HTTP only (never ProjectReference).
- Uses `qyl.contracts` types where shared contracts are required.
- Uses `McpToolRegistry`, `UseQylTools`, and `RcaTools` for embedded meta-agent flows.

## Constraints
- Must not reference qyl.collector project directly
- All collector communication via HTTP REST API
- ModelContextProtocol SDK for transport layer
- `QYL_SKILLS` gates which tool families are registered at startup
- Do not reintroduce a parallel investigation/proxy stack; broad queries should go through `qyl.use_qyl` or focused agent tools over the same DI-resolved tool set
