# qyl Spec Status

> Last updated: 2026-03-22 (verified by 8 parallel agents with file:line evidence)

## Implementation Status

| Spec | Status | Done | Remaining |
|------|--------|------|-----------|
| [Architecture](./00-architecture.md) | **DONE** | All product + architecture criteria | UX perf benchmarks (2 items) |
| [Collector](./collector.md) | **DONE** | 9/9 | — |
| [Contracts](./contracts.md) | **DONE** | 4/4 | — |
| [Instrumentation](./instrumentation.md) | **DONE** | 8/8 | — |
| [Cost](./cost.md) | **PARTIAL** | 6/8 | Budget alert firings, historical recompute job |
| [Dashboard](./dashboard.md) | **PARTIAL** | 5/11 | TanStack Table migration (4 pages), keyboard nav, realtime handoff, AI/fact distinction, accessibility |
| [MCP](./mcp.md) | **PARTIAL** | 6/12 | toolVersion (SDK blocked), cursor pagination (4 tools), 25K enforcement, interactive apps, entity IDs, evidence citations |
| [Telemetry Intelligence](./telemetry-intelligence.md) | **PARTIAL** | 9/11 | docs/intelligence-model.md, Loom pipeline integration |
| [Telemetry Data Model](./telemetry-data-model.md) | **DONE** | Reference spec | — |
| [Issue Fingerprinting](./issue-fingerprinting.md) | **DONE** | Reference spec | — |
| [Loom](./loom.md) | **REFERENCE** | Standalone product | ~/RiderProjects/qyl.loom/ |
| [API](./api.md) | **DONE** | Contract spec | — |

## Open Work Items

1. **Budget alert firings** — wire cost threshold to `alert_firings` table + SSE event (cost.md)
2. **Historical cost recompute** — update `gen_ai_cost_usd` when pricing changes retroactively (cost.md)
3. **TanStack Table migration** — TracesPage, LogsPage, IssuesPage, ErrorsOutagesPage need TanStack Table (dashboard.md)
4. **MCP cursor pagination** — list_services, list_metrics, list_error_issues, list_triage lack cursor (mcp.md)
5. **MCP 25K token enforcement** — add hard truncation, not just implicit page sizes (mcp.md)
6. **docs/intelligence-model.md** — generate from TypeSpec (telemetry-intelligence.md)
7. **Loom pattern engine integration** — Stage 0 invokes PatternEngine before LLM (telemetry-intelligence.md)
8. **Dashboard keyboard nav audit** — partial coverage, not fully audited (dashboard.md)
9. **Dashboard accessibility audit** — verify semantics across all components (dashboard.md)
10. **AI/fact visual distinction** — AI analysis must look different from raw telemetry (dashboard.md)
11. **Realtime handoff streams** — SSE reconnect exists, handoff event type missing (dashboard.md)
12. **MCP interactive apps** — verify in Claude Desktop (mcp.md)
13. **MCP entity ID consistency** — audit cross-tool chaining (mcp.md)
14. **MCP evidence citations** — add to analysis responses (mcp.md)
15. **MCP toolVersion** — blocked by SDK (mcp.md)
16. **UX perf benchmarks** — setup time, dashboard load with 100K spans (00-architecture.md)

## Decisions (permanent)

| Decision | Status |
|----------|--------|
| [No Proxy Gateway](./decisions/no-proxy.md) | Accepted |
| [No Helicone Sidecar](./decisions/no-helicone.md) | Accepted |
| [Loom as Standalone Product](./decisions/loom-standalone.md) | Accepted |
| [MAF Native Migration](./decisions/maf-native-migration.md) | Accepted, executed 2026-03-16 |
