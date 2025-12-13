# qyl.mcp — MCP Server

@../../CLAUDE.md

## Purpose

Model Context Protocol (MCP) server that enables AI agents to query qyl:
- Search spans with filters
- Analyze token usage and costs
- Get session summaries
- Explore trace trees

**Communicates with collector via HTTP only** — no direct database access.

## Hard Rules

| ✅ MAY Reference | ❌ MUST NOT Reference |
|-----------------|----------------------|
| `qyl.protocol` | `qyl.collector` (NO project ref!) |
| `ModelContextProtocol` | `qyl.dashboard` |
| `System.Net.Http` | `DuckDB.*` |
| `Microsoft.Extensions.Http` | Any storage packages |

**Critical**: MCP talks to collector via HTTP REST API. This enables:
- Independent deployment
- Collector can be replaced/upgraded without MCP changes
- Clear API contract boundary

## Structure

```
qyl.mcp/
├── qyl.mcp.csproj
├── Program.cs                    # Host setup, MCP registration
├── Client/
│   └── QylCollectorClient.cs     # Typed HTTP client
├── Tools/
│   ├── QuerySpansTool.cs         # query_spans
│   ├── GetSessionTool.cs         # get_session
│   ├── GetTraceTool.cs           # get_trace
│   ├── AnalyzeGenAiTool.cs       # analyze_genai
│   ├── ListServicesTool.cs       # list_services
│   └── CompareSessionsTool.cs    # compare_sessions
└── appsettings.json
```

## MCP Tools

### query_spans
Search spans with filters.

| Parameter | Type | Description |
|-----------|------|-------------|
| `serviceName` | string? | Filter by service |
| `from` | string? | Start time (ISO 8601) |
| `to` | string? | End time (ISO 8601) |
| `genAiOnly` | bool? | Only gen_ai spans |
| `limit` | int | Max results (default: 50) |

### get_session
Get session details.

| Parameter | Type | Description |
|-----------|------|-------------|
| `sessionId` | string | Session ID |

### get_trace
Get full trace tree.

| Parameter | Type | Description |
|-----------|------|-------------|
| `traceId` | string | Trace ID (32 hex chars) |

### analyze_genai
Analyze gen_ai usage and costs.

| Parameter | Type | Description |
|-----------|------|-------------|
| `period` | string | Time period: 1h, 24h, 7d |
| `groupBy` | string | Group by: model, provider, service |

### list_services
List discovered services. No parameters.

### compare_sessions
Compare two sessions.

| Parameter | Type | Description |
|-----------|------|-------------|
| `sessionId1` | string | First session |
| `sessionId2` | string | Second session |

## Key Components

### QylCollectorClient
Typed HTTP client using `IHttpClientFactory`:

```csharp
public sealed class QylCollectorClient(HttpClient http)
{
    public async Task<IReadOnlyList<SpanRecord>> QuerySpansAsync(
        string? serviceName = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        bool? genAiOnly = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (serviceName is not null) query.Add($"serviceName={serviceName}");
        if (from is not null) query.Add($"from={from:O}");
        if (to is not null) query.Add($"to={to:O}");
        if (genAiOnly == true) query.Add("genAiOnly=true");
        query.Add($"limit={limit}");

        var url = $"/api/v1/spans?{string.Join("&", query)}";
        return await http.GetFromJsonAsync<List<SpanRecord>>(url, ct) ?? [];
    }
}
```

### Tool Implementation
```csharp
[McpTool("query_spans", "Search spans with filters")]
public sealed class QuerySpansTool(QylCollectorClient client)
{
    [McpToolMethod]
    public async Task<string> ExecuteAsync(
        [Description("Service name filter")] string? serviceName = null,
        [Description("Start time (ISO 8601)")] string? from = null,
        [Description("End time (ISO 8601)")] string? to = null,
        [Description("Only gen_ai spans")] bool? genAiOnly = null,
        [Description("Max results")] int limit = 50)
    {
        var spans = await client.QuerySpansAsync(
            serviceName: serviceName,
            from: from is not null ? DateTimeOffset.Parse(from) : null,
            to: to is not null ? DateTimeOffset.Parse(to) : null,
            genAiOnly: genAiOnly,
            limit: limit);

        return FormatAsMarkdown(spans);
    }
}
```

### Program.cs
```csharp
var builder = Host.CreateApplicationBuilder(args);

// HTTP client to collector
builder.Services.AddHttpClient<QylCollectorClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Collector:Endpoint"] ?? "http://localhost:8080");
});

// MCP server with stdio transport
builder.Services.AddMcpServer()
    .WithStdioTransport()
    .WithTools<QuerySpansTool>()
    .WithTools<GetSessionTool>()
    .WithTools<GetTraceTool>()
    .WithTools<AnalyzeGenAiTool>()
    .WithTools<ListServicesTool>()
    .WithTools<CompareSessionsTool>();

await builder.Build().RunAsync();
```

## Configuration

```json
{
  "Collector": {
    "Endpoint": "http://localhost:8080"
  }
}
```

Environment variable: `Collector__Endpoint=http://qyl-collector:8080`

## Output Formatting

Tools return Markdown-formatted responses for AI readability:

```csharp
private static string FormatAsMarkdown(IReadOnlyList<SpanRecord> spans)
{
    var sb = new StringBuilder();
    sb.AppendLine($"## Found {spans.Count} spans\n");
    
    foreach (var span in spans)
    {
        sb.AppendLine($"### {span.Name}");
        sb.AppendLine($"- **TraceId**: `{span.TraceId}`");
        sb.AppendLine($"- **Duration**: {span.Duration.TotalMilliseconds:F1}ms");
        
        if (span.GenAi is { } genAi)
        {
            sb.AppendLine($"- **Model**: {genAi.RequestModel}");
            sb.AppendLine($"- **Tokens**: {genAi.InputTokens} in / {genAi.OutputTokens} out");
        }
        sb.AppendLine();
    }
    
    return sb.ToString();
}
```

## Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "qyl.mcp.dll"]
```

## Claude Desktop Integration

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/qyl.mcp"],
      "env": {
        "Collector__Endpoint": "http://localhost:8080"
      }
    }
  }
}
```
