# qyl.mcp

MCP Server for AI agent integration. HTTP client to collector.

## identity

```yaml
name: qyl.mcp
type: console-app
sdk: ANcpLua.NET.Sdk
protocol: model-context-protocol
transport: stdio
```

## connection

```yaml
to-collector:
  method: http
  base-url: http://localhost:5100
  endpoints-used:
    - /api/v1/sessions
    - /api/v1/sessions/{id}
    - /api/v1/sessions/{id}/spans
    - /api/v1/traces/{traceId}

forbidden:
  - project-reference to qyl.collector
  - direct DuckDB access
  reason: must remain decoupled, http-only
```

## mcp-tools

```yaml
tools:
  - name: qyl.search_agent_runs
    description: Search for agent runs by time range or filters
    parameters:
      - name: start_time
        type: datetime
        optional: true
      - name: end_time
        type: datetime
        optional: true
      - name: service_name
        type: string
        optional: true
        
  - name: qyl.get_agent_run
    description: Get details of a specific session
    parameters:
      - name: session_id
        type: string
        required: true
        
  - name: qyl.get_token_usage
    description: Get token usage statistics
    parameters:
      - name: session_id
        type: string
        optional: true
      - name: time_range
        type: string
        optional: true
        
  - name: qyl.list_errors
    description: List error spans
    parameters:
      - name: session_id
        type: string
        optional: true
      - name: limit
        type: int
        default: 10
        
  - name: qyl.get_latency_stats
    description: Get latency percentiles
    parameters:
      - name: session_id
        type: string
        optional: true
```

## http-client-pattern

```yaml
pattern: |
  public class QylClient(HttpClient http)
  {
      public async Task<SessionSummary[]> GetSessionsAsync(CancellationToken ct = default)
      {
          var response = await http.GetAsync("/api/v1/sessions", ct);
          response.EnsureSuccessStatusCode();
          return await response.Content.ReadFromJsonAsync<SessionSummary[]>(ct);
      }
  }

registration: |
  services.AddHttpClient<QylClient>(client =>
  {
      client.BaseAddress = new Uri("http://localhost:5100");
  });
```

## dependencies

```yaml
project-references:
  - qyl.protocol
  
packages:
  - Microsoft.Extensions.Http
  - System.Text.Json

forbidden:
  - qyl.collector
  - DuckDB.NET.Data.Full
```

## invocation

```yaml
standalone: dotnet run --project src/qyl.mcp
with-claude: claude --mcp qyl
```
