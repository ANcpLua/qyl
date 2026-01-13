# qyl.mcp

MCP Server exposing telemetry query tools to AI agents via stdio transport.

## Architecture Position

```
qyl.collector ◄──HTTP── qyl.mcp ──stdio──► Claude/AI Agent
```

## CRITICAL: HTTP-ONLY Communication

**qyl.mcp MUST communicate with qyl.collector via HTTP. No ProjectReference to collector allowed.**

### Correct Pattern

```csharp
public sealed class QylCollectorClient(HttpClient http)
{
    private const string BaseUrl = "http://localhost:5100";

    public async Task<AgentRun[]> SearchRunsAsync(
        string? provider, string? model, string? errorType, DateTime? since,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/runs?provider={provider}&model={model}";
        return await http.GetFromJsonAsync<AgentRun[]>(url, ct) ?? [];
    }
}
```

### Current Violation (MUST FIX)

`Program.cs` line 11 registers `InMemoryTelemetryStore.Instance` instead of HTTP client:

```csharp
// WRONG - violates architecture
builder.Services.AddSingleton<ITelemetryStore>(InMemoryTelemetryStore.Instance);

// CORRECT - use HTTP client
builder.Services.AddHttpClient<QylCollectorClient>(c => c.BaseAddress = new("http://localhost:5100"));
builder.Services.AddSingleton<ITelemetryStore, HttpTelemetryStore>();
```

## MCP Tools Exposed

| Tool                    | Purpose                             |
|-------------------------|-------------------------------------|
| `qyl.search_agent_runs` | Search runs by provider/model/error |
| `qyl.get_agent_run`     | Get single run by ID                |
| `qyl.get_token_usage`   | Token usage grouped by agent/model  |
| `qyl.list_errors`       | Recent errors with stack traces     |
| `qyl.get_latency_stats` | P50/P95/P99 latency percentiles     |

## Known Issues

| ID       | Severity | Description                                     |
|----------|----------|-------------------------------------------------|
| ARCH-002 | CRITICAL | Uses InMemoryStore instead of HTTP to collector |
| TEST-001 | HIGH     | 0% test coverage - test project has no tests    |
| ERR-001  | MEDIUM   | No retry logic for HTTP failures                |
| ERR-002  | MEDIUM   | Silent exception swallowing in tool handlers    |

## Files

| File                            | Purpose                                |
|---------------------------------|----------------------------------------|
| `Program.cs`                    | Host setup, MCP server registration    |
| `Client.cs`                     | A2A agent client, telemetry decorators |
| `Tools/TelemetryTools.cs`       | MCP tool definitions + ITelemetryStore |
| `Tools/TelemetryJsonContext.cs` | AOT-compatible JSON serialization      |

## Run

```bash
dotnet run --project src/qyl.mcp
```

Connects via stdio - designed for Claude Desktop or AI agent orchestration.
