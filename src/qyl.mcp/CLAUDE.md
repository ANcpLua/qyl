# qyl.mcp - MCP Server

Model Context Protocol server for AI assistant integration.

## Identity

| Property  | Value                |
|-----------|----------------------|
| SDK       | ANcpLua.NET.Sdk      |
| Framework | net10.0              |
| AOT       | **Yes**              |
| Protocol  | stdio (MCP standard) |

## MCP Tools

### Session & Trace Analysis

| Tool                          | Purpose                                       |
|-------------------------------|-----------------------------------------------|
| `qyl.list_sessions`           | List AI sessions with span/error counts       |
| `qyl.get_session_transcript`  | Human-readable timeline of a session          |
| `qyl.get_trace`               | Get complete span tree for a trace            |
| `qyl.analyze_session_errors`  | Analyze all errors in a session               |

### GenAI Analytics

| Tool                       | Purpose                              |
|----------------------------|--------------------------------------|
| `qyl.get_genai_stats`      | Token usage, costs, latency summary  |
| `qyl.list_genai_spans`     | Query LLM calls with filters         |
| `qyl.list_models`          | Usage breakdown by model             |
| `qyl.get_token_timeseries` | Token consumption over time          |

### Agent Run Telemetry

| Tool                     | Purpose                             |
|--------------------------|-------------------------------------|
| `qyl.search_agent_runs`  | Search agent runs by provider/model |
| `qyl.get_agent_run`      | Get details of a specific run       |
| `qyl.get_token_usage`    | Aggregated token usage by group     |
| `qyl.list_errors`        | Recent errors from agent runs       |
| `qyl.get_latency_stats`  | Latency percentiles (P50/P95/P99)   |

### Console Logs (Frontend)

| Tool                      | Purpose                          |
|---------------------------|----------------------------------|
| `qyl.list_console_logs`   | Frontend console.log messages    |
| `qyl.list_console_errors` | Frontend console errors/warnings |

### Structured Logs (OTLP)

| Tool                       | Purpose                        |
|----------------------------|--------------------------------|
| `qyl.list_structured_logs` | Query OTLP log records         |
| `qyl.list_trace_logs`      | Get logs for a specific trace  |
| `qyl.search_logs`          | Search logs by text pattern    |

### Storage & Health

| Tool                    | Purpose                    |
|-------------------------|----------------------------|
| `qyl.get_storage_stats` | Database statistics        |
| `qyl.health_check`      | Collector health status    |
| `qyl.search_spans`      | General-purpose span query |

## Architecture

```
AI Assistant (Claude, GPT, etc.)
         |
         | MCP (stdio JSON-RPC)
         v
    qyl.mcp
         |
         | HTTP
         v
  qyl.collector
```

**Important**: MCP server communicates with collector via HTTP only. No direct database access.

## Usage

### Claude Desktop Configuration

```json
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "src/qyl.mcp"]
    }
  }
}
```

### Direct Invocation

```bash
# Run MCP server (stdio mode)
dotnet run --project src/qyl.mcp
```

## Tool Files

| File                     | Purpose                              |
|--------------------------|--------------------------------------|
| `Tools/TelemetryTools.cs`     | Agent run telemetry queries     |
| `Tools/ReplayTools.cs`        | Session replay and analysis     |
| `Tools/ConsoleTools.cs`       | Frontend console log access     |
| `Tools/StructuredLogTools.cs` | OTLP structured log queries     |
| `Tools/GenAiTools.cs`         | GenAI-specific analytics        |
| `Tools/StorageTools.cs`       | Health check and span search    |
| `Tools/HttpTelemetryStore.cs` | HTTP client for collector API   |

## Tool Descriptions

All tools include detailed descriptions optimized for AI agents:
- Clear explanation of what the tool does
- Example queries showing common use cases
- Parameter descriptions with valid values
- Return value format descriptions

## Dependencies

### Project References

- `qyl.protocol` - Shared types

### Packages

- `ModelContextProtocol` - MCP SDK

## Environment Variables

| Variable            | Default               | Purpose                               |
|---------------------|-----------------------|---------------------------------------|
| `QYL_COLLECTOR_URL` | http://localhost:5100 | Collector API URL                     |
| `QYL_MCP_TOKEN`     | (none)                | API key for collector authentication  |

## Authentication

The MCP server supports API key authentication when communicating with qyl.collector.

### Configuration

Set the `QYL_MCP_TOKEN` environment variable to enable authentication:

```bash
# In Claude Desktop config
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "src/qyl.mcp"],
      "env": {
        "QYL_MCP_TOKEN": "your-collector-token-here"
      }
    }
  }
}
```

### Behavior

- **Token configured**: Adds `x-mcp-api-key` header to all collector requests (Aspire pattern)
- **No token**: Auth disabled, runs in dev mode without authentication
- Uses the same token as configured in collector via `QYL_TOKEN`

### Files

| File | Purpose |
|------|---------|
| `Auth/McpAuthOptions.cs` | Configuration options |
| `Auth/McpAuthHandler.cs` | HTTP delegating handler |
| `Auth/McpAuthExtensions.cs` | DI registration helpers |

## Rules

- **No ProjectReference to collector** - must use HTTP
- All data access via collector REST API
- Use stdio transport for MCP compliance
- AOT-compatible code only
