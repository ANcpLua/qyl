# qyl.mcp - MCP Server

Model Context Protocol server for AI assistant integration.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | net10.0 |
| AOT | **Yes** |
| Protocol | stdio (MCP standard) |

## MCP Tools

| Tool | Purpose |
|------|---------|
| `query_spans` | Search spans with filters |
| `get_session` | Get session details |
| `get_trace` | Get full trace tree |
| `analyze_tokens` | Token usage analysis |

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

## Tool Schemas

### query_spans

```json
{
  "name": "query_spans",
  "description": "Search telemetry spans",
  "inputSchema": {
    "type": "object",
    "properties": {
      "service_name": { "type": "string" },
      "operation": { "type": "string" },
      "start_time": { "type": "string", "format": "date-time" },
      "end_time": { "type": "string", "format": "date-time" },
      "limit": { "type": "integer", "default": 100 }
    }
  }
}
```

### get_trace

```json
{
  "name": "get_trace",
  "description": "Get complete trace tree by ID",
  "inputSchema": {
    "type": "object",
    "properties": {
      "trace_id": { "type": "string" }
    },
    "required": ["trace_id"]
  }
}
```

## Dependencies

### Project References

- `qyl.protocol` - Shared types

### Packages

- `ModelContextProtocol` - MCP SDK

## Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `QYL_COLLECTOR_URL` | http://localhost:5100 | Collector API URL |

## Rules

- **No ProjectReference to collector** - must use HTTP
- All data access via collector REST API
- Use stdio transport for MCP compliance
- AOT-compatible code only
