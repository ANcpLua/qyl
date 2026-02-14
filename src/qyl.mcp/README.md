# qyl.mcp

MCP (Model Context Protocol) server for the qyl AI observability platform. Gives AI agents native access to telemetry data — traces, logs, metrics, GenAI sessions — over stdio.

## Install

```bash
dotnet tool install --global qyl.mcp
```

## Configure

Set environment variables before launching:

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_COLLECTOR_URL` | `http://localhost:5100` | Collector endpoint |
| `QYL_MCP_TOKEN` | *(none)* | Auth token |

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
| `qyl.list_console_logs` | Frontend console.log entries |
| `qyl.list_console_errors` | Frontend console errors |
| `qyl.list_structured_logs` | OTLP log records |
| `qyl.list_trace_logs` | Logs for a specific trace |
| `qyl.search_logs` | Search logs by text |
| `qyl.get_storage_stats` | Database statistics |
| `qyl.health_check` | Collector health |
| `qyl.search_spans` | General span query |

## Transport

stdio (JSON-RPC) — compatible with Claude Code, Cursor, and any MCP-compliant client.

## Links

- [qyl repository](https://github.com/ANcpLua/qyl)
- [MCP specification](https://modelcontextprotocol.io)
