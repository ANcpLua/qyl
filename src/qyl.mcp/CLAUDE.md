# qyl.mcp

MCP (Model Context Protocol) server for AI assistant integration.

## identity

```yaml
sdk: ANcpLua.NET.Sdk
protocol: stdio (MCP standard)
connection: HTTP to collector only
```

## tools

```yaml
query_spans: Search spans with filters
get_session: Get session details
get_trace: Get full trace tree
analyze_tokens: Token usage analysis
```

## architecture

```yaml
transport: stdio (stdin/stdout JSON-RPC)
backend: HTTP calls to collector REST API
no-direct-db: Must go through collector API
```

## dependencies

```yaml
project: qyl.protocol
packages:
  - ModelContextProtocol
```

## usage

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

## rules

- NO ProjectReference to collector (HTTP only)
- All data access via collector REST API
- Stdio transport for MCP compliance
