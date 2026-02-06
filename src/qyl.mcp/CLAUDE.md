# qyl.mcp - MCP Server

Model Context Protocol server for AI assistant integration. AOT-compatible, stdio transport.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | net10.0 |
| AOT | Yes |
| Protocol | stdio (MCP JSON-RPC) |

## Tools

| Tool | Purpose |
|------|---------|
| `qyl.list_sessions` | List AI sessions with span/error counts |
| `qyl.get_session_transcript` | Human-readable session timeline |
| `qyl.get_trace` | Complete span tree for a trace |
| `qyl.analyze_session_errors` | Analyze session errors |
| `qyl.get_genai_stats` | Token usage, costs, latency |
| `qyl.list_genai_spans` | Query LLM calls with filters |
| `qyl.list_models` | Model usage breakdown |
| `qyl.get_token_timeseries` | Token consumption over time |
| `qyl.search_agent_runs` | Search agent runs |
| `qyl.get_agent_run` | Agent run details |
| `qyl.get_token_usage` | Aggregated token usage |
| `qyl.list_errors` | Recent agent errors |
| `qyl.get_latency_stats` | P50/P95/P99 latency |
| `qyl.list_console_logs` | Frontend console.log |
| `qyl.list_console_errors` | Frontend console errors |
| `qyl.list_structured_logs` | OTLP log records |
| `qyl.list_trace_logs` | Logs for a trace |
| `qyl.search_logs` | Search logs by text |
| `qyl.get_storage_stats` | Database statistics |
| `qyl.health_check` | Collector health |
| `qyl.search_spans` | General span query |

## Files

| File | Purpose |
|------|---------|
| `Tools/GenAiTools.cs` | GenAI analytics |
| `Tools/ReplayTools.cs` | Session replay/analysis |
| `Tools/ConsoleTools.cs` | Frontend console logs |
| `Tools/StructuredLogTools.cs` | OTLP structured logs |
| `Tools/StorageTools.cs` | Health + span search |
| `Tools/CopilotTools.cs` | Copilot integration |
| `Client.cs` | HTTP client for collector API |

## Environment

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_COLLECTOR_URL` | http://localhost:5100 | Collector URL |
| `QYL_MCP_TOKEN` | (none) | Auth token |

## Rules

- No ProjectReference to collector — HTTP only
- AOT-compatible code only
- stdio transport for MCP compliance
