# Query — Requirements

Extracted from [loom-design.md §22](../roadmap/loom-design.md#22-requirements-registry).

## Requirements

| ID      | Capability                     | Domain   | Scope                | Evidence                                    | Verification                                     |
|---------|--------------------------------|----------|----------------------|---------------------------------------------|--------------------------------------------------|
| QYL-003 | MCP Platform (60+ tools)       | MCP      | `IMPLEMENTED-IN-QYL` | `QylSkillKind` enum, `QYL_SKILLS` env var   | Invoke tools via MCP protocol, verify responses  |
| QYL-004 | AI Chat Analytics (6 modules)  | Analytics| `IMPLEMENTED-IN-QYL` | 6 modules, 9 API endpoints                  | API returns real data for each module            |
| QYL-005 | AG-UI + Declarative Workflows  | Protocol | `IMPLEMENTED-IN-QYL` | QylAgentBuilder, DeclarativeEngine, SSE      | SSE stream contract verified with CopilotKit     |
| QYL-013 | MCP Platform Extended          | MCP      | `CONTEXT-ONLY`       | Monolith split, Streamable HTTP, OAuth       | Roadmap — not ship-blocking                      |

## Sentry Reference (CONTEXT-ONLY)

| ID      | Capability                 | Scope          |
|---------|----------------------------|----------------|
| MCP-001 | Tool Catalog (27 tools)    | `CONTEXT-ONLY` |
| MCP-002 | Skills Authorization (5)   | `CONTEXT-ONLY` |
| MCP-003 | Embedded Agent Framework   | `CONTEXT-ONLY` |
| MCP-004 | Dual OAuth Architecture    | `CONTEXT-ONLY` |
| MCP-005 | Deployment Models (2)      | `CONTEXT-ONLY` |

## Acceptance Criteria

- [ ] MCP tools callable via protocol (not just registered)
- [ ] AG-UI SSE stream delivers events to CopilotKit frontend
- [ ] Analytics API returns real data for all 6 modules
- [ ] AssistedQueryTools translates NL → DuckDB query correctly
- [ ] TestGenerationTools produces valid test suggestions
