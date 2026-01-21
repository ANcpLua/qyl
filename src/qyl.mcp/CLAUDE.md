# qyl.mcp

MCP server exposing qyl telemetry to AI assistants.

## identity

```yaml
name: qyl.mcp
type: mcp-server
sdk: ANcpLua.NET.Sdk
transport: stdio
role: ai-integration-layer
```

## architecture

```yaml
communication:
  to: qyl.collector
  via: http-only
  reason: decoupled (no ProjectReference to collector)

connection:
  env: QYL_COLLECTOR_URL
  default: http://localhost:5100
  resilience: standard-handler (retry + circuit-breaker)
  timeout: 30s
```

## tools

```yaml
telemetry-tools:
  - name: qyl.search_agent_runs
    purpose: Search agent runs by provider, model, error, time
    params: [provider?, model?, errorType?, since?]

  - name: qyl.get_agent_run
    purpose: Get specific agent run details
    params: [runId]

  - name: qyl.get_token_usage
    purpose: Token statistics with grouping
    params: [since?, until?, groupBy=agent|model|hour]

  - name: qyl.list_errors
    purpose: Recent errors from agent runs
    params: [limit=50, agentName?]

  - name: qyl.get_latency_stats
    purpose: Latency percentiles (P50, P95, P99)
    params: [agentName?, hours=24]

replay-tools:
  - name: qyl.list_sessions
    purpose: List available sessions for replay
    params: [limit=20, serviceName?]

  - name: qyl.get_session_transcript
    purpose: Human-readable session transcript
    params: [sessionId]

  - name: qyl.get_trace
    purpose: Trace details with span hierarchy
    params: [traceId]

  - name: qyl.analyze_session_errors
    purpose: Error analysis for a session
    params: [sessionId]
```

## collector-api

```yaml
endpoints-used:
  - GET /api/v1/sessions
  - GET /api/v1/sessions/{id}
  - GET /api/v1/sessions/{id}/spans
  - GET /api/v1/traces/{traceId}

response-format: snake_case JSON
json-context: source-generated (AOT-safe)
```

## patterns

```yaml
http-client:
  registration: |
    builder.Services.AddHttpClient<ReplayTools>(client =>
    {
        client.BaseAddress = new Uri(collectorUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

mcp-setup:
  registration: |
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<TelemetryTools>(jsonOptions)
        .WithTools<ReplayTools>(jsonOptions);

tool-result:
  pattern: |
    public record ToolResult<T>(bool Success, T? Data, string? Error);
    public static ToolResult<T> Ok<T>(T data) => new(true, data, null);
    public static ToolResult<T> Error<T>(string msg) => new(false, default, msg);

error-handling:
  pattern: |
    try { ... }
    catch (HttpRequestException ex)
    {
        return ToolResult.Error<T>($"Failed to connect to qyl collector: {ex.Message}");
    }

time:
  provider: TimeProvider.System
  injection: constructor (for testability)
```

## dependencies

```yaml
project-references:
  - qyl.protocol (types only)

packages:
  - ModelContextProtocol.Server
  - Microsoft.Extensions.Http.Resilience

forbidden:
  - qyl.collector (must use HTTP)
```

## usage

```yaml
claude-desktop:
  config: |
    {
      "mcpServers": {
        "qyl": {
          "command": "dotnet",
          "args": ["run", "--project", "src/qyl.mcp"],
          "env": {
            "QYL_COLLECTOR_URL": "http://localhost:5100"
          }
        }
      }
    }

standalone:
  command: dotnet run --project src/qyl.mcp

with-collector:
  prerequisite: qyl collector must be running
  start-collector: dotnet run --project src/qyl.collector
```
