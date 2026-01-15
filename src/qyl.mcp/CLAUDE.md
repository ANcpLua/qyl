# qyl.mcp

MCP Server for AI agent self-introspection.

## identity

```yaml
name: qyl.mcp
type: console-app
sdk: ANcpLua.NET.Sdk
protocol: model-context-protocol
transport: stdio
purpose: let agents query their own telemetry
```

## connection

```yaml
to-collector:
  method: http-only
  base-url: http://localhost:5100
  endpoints:
    - /api/v1/sessions
    - /api/v1/sessions/{id}
    - /api/v1/sessions/{id}/spans
    - /api/v1/traces/{traceId}
    - /api/v1/stats/tokens
    - /api/v1/stats/latency

forbidden:
  - ProjectReference to qyl.collector
  - Direct DuckDB access
  reason: must remain decoupled
```

## mcp-tools

```yaml
tools:
  qyl.search_agent_runs:
    description: Search for agent runs by time range or filters
    parameters:
      start_time: datetime (optional)
      end_time: datetime (optional)
      service_name: string (optional)
    returns: SessionSummary[]
    
  qyl.get_agent_run:
    description: Get details of a specific session
    parameters:
      session_id: string (required)
    returns: SessionSummary + SpanRecord[]
    
  qyl.get_token_usage:
    description: Get token usage statistics
    parameters:
      session_id: string (optional)
      time_range: string (optional, e.g. "1h", "24h", "7d")
    returns: { input_tokens, output_tokens, cost_usd }
    
  qyl.list_errors:
    description: List error spans
    parameters:
      session_id: string (optional)
      limit: int (default: 10)
    returns: SpanRecord[] where status_code = ERROR
    
  qyl.get_latency_stats:
    description: Get latency percentiles
    parameters:
      session_id: string (optional)
      operation: string (optional)
    returns: { p50, p90, p99, avg, min, max }
    
  qyl.get_span_tree:
    description: Get hierarchical trace view
    parameters:
      trace_id: string (required)
    returns: TraceNode (recursive)
```

## http-client

```yaml
pattern: |
  public sealed class QylClient(HttpClient http)
  {
      public async Task<SessionSummary[]> GetSessionsAsync(
          DateTimeOffset? start = null,
          DateTimeOffset? end = null,
          string? serviceName = null,
          CancellationToken ct = default)
      {
          var query = new List<string>();
          if (start.HasValue) query.Add($"start={start.Value:O}");
          if (end.HasValue) query.Add($"end={end.Value:O}");
          if (serviceName != null) query.Add($"service={Uri.EscapeDataString(serviceName)}");
          
          var url = query.Count > 0 
              ? $"/api/v1/sessions?{string.Join("&", query)}"
              : "/api/v1/sessions";
              
          var response = await http.GetAsync(url, ct);
          response.EnsureSuccessStatusCode();
          return await response.Content.ReadFromJsonAsync<SessionSummary[]>(s_options, ct) 
              ?? [];
      }
      
      // ... other methods ...
      
      private static readonly JsonSerializerOptions s_options = new()
      {
          PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
      };
  }

registration: |
  services.AddHttpClient<QylClient>(client =>
  {
      var baseUrl = Environment.GetEnvironmentVariable("QYL_URL") 
          ?? "http://localhost:5100";
      client.BaseAddress = new Uri(baseUrl);
  });
```

## mcp-server-pattern

```yaml
entry: |
  var builder = McpServer.CreateBuilder();
  
  builder.AddTool("qyl.search_agent_runs", 
      "Search for agent runs by time range or filters",
      async (params, ct) => 
      {
          var client = services.GetRequiredService<QylClient>();
          var sessions = await client.GetSessionsAsync(
              params.GetDateTime("start_time"),
              params.GetDateTime("end_time"),
              params.GetString("service_name"),
              ct);
          return McpResult.Json(sessions);
      });
  
  // ... other tools ...
  
  await builder.Build().RunAsync();
```

## dependencies

```yaml
project-references:
  - qyl.protocol

packages:
  - Microsoft.Extensions.Http
  - System.Text.Json
  # MCP SDK when available

forbidden:
  - qyl.collector
  - DuckDB.NET.Data.Full
```

## invocation

```yaml
standalone: |
  dotnet run --project src/qyl.mcp
  
with-claude: |
  # In Claude's MCP config
  {
    "mcpServers": {
      "qyl": {
        "command": "dotnet",
        "args": ["run", "--project", "/path/to/qyl.mcp"]
      }
    }
  }

environment:
  QYL_URL: http://localhost:5100 (default)
```

## use-case

```yaml
scenario: |
  Claude is working on code. It makes API calls. 
  Later, Claude can ask qyl.mcp:
  
  "What did I do in the last hour?"
  → qyl.search_agent_runs(start_time=now-1h)
  
  "How many tokens did I use?"
  → qyl.get_token_usage(time_range="1h")
  
  "Were there any errors?"
  → qyl.list_errors(limit=5)
  
  "Show me the trace for that failed request"
  → qyl.get_span_tree(trace_id="abc123...")
```
