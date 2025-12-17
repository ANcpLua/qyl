# ADR-0007: VS-06 MCP Query Tool

## Metadata

| Field      | Value                              |
|------------|------------------------------------|
| Status     | Draft                              |
| Date       | 2025-12-16                         |
| Slice      | VS-06                              |
| Priority   | P2                                 |
| Depends On | ADR-0002 (VS-01), ADR-0003 (VS-02) |
| Supersedes | -                                  |

## Context

AI Agents (Claude, GPT) sollen qyl Telemetrie-Daten abfragen können via Model Context Protocol (MCP). Dies ermöglicht:

- AI-gestütztes Debugging ("Warum ist dieser Request langsam?")
- Automatisierte Reports ("Wie war die Performance letzte Woche?")
- Natural Language Queries ("Zeige mir alle Errors von Service X")

## Decision

Implementierung eines vollständigen MCP Server mit:

- Stdio Transport (Standard für MCP)
- HTTP Client zu qyl.collector (keine direkte DB-Verbindung)
- Tools für Query, Analyse und Vergleich
- Markdown-formatierte Outputs für AI Agents

## Layers

### 1. Protocol Layer

```yaml
files:
  - src/qyl.protocol/Models/SpanRecord.cs      # Shared with collector
  - src/qyl.protocol/Models/SessionSummary.cs  # Shared with collector
note: "MCP verwendet dieselben Models wie Collector"
```

### 2. Client Layer

```yaml
files:
  - src/qyl.mcp/Client.cs    # QylCollectorClient (HttpClient wrapper)
methods:
  - "GetSessionsAsync(limit, serviceName)"
  - "GetSessionAsync(sessionId)"
  - "GetTraceAsync(traceId)"
  - "GetSpansAsync(query)"
  - "GetGenAiStatsAsync(timeRange)"
patterns:
  - "StandardResilienceHandler (Polly) for retries"
  - "Circuit breaker for collector outages"
```

**Client Implementation:**

```csharp
public sealed class QylCollectorClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public QylCollectorClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
    }

    public async Task<SessionSummary[]> GetSessionsAsync(
        int limit = 10,
        string? serviceName = null,
        CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/api/v1/sessions?limit={limit}";
        if (serviceName is not null)
            url += $"&serviceName={Uri.EscapeDataString(serviceName)}";

        var response = await _http.GetFromJsonAsync<SessionListResponse>(url, ct);
        return response?.Sessions ?? [];
    }

    // ... weitere Methoden
}
```

### 3. MCP Tools Layer

```yaml
files:
  - src/qyl.mcp/Tools/TelemetryTools.cs  # All MCP tools in one class
tools:
  - name: "query_spans"
    description: "Query spans with filters"
    parameters:
      serviceName: string (optional)
      traceId: string (optional)
      sessionId: string (optional)
      from: datetime (optional)
      to: datetime (optional)
      limit: int (default: 20)

  - name: "get_session"
    description: "Get session details with GenAI stats"
    parameters:
      sessionId: string (required)

  - name: "get_trace"
    description: "Get trace tree with timing"
    parameters:
      traceId: string (required)

  - name: "list_services"
    description: "List all known services"

  - name: "analyze_genai"
    description: "Analyze GenAI usage and costs"
    parameters:
      timeRange: "1h" | "24h" | "7d" | "30d"

  - name: "compare_sessions"
    description: "Compare two sessions"
    parameters:
      sessionId1: string (required)
      sessionId2: string (required)
```

### 4. MCP Server

```yaml
files:
  - src/qyl.mcp/Program.cs    # MCP server entry point
entry_point: |
  var builder = Host.CreateApplicationBuilder(args);

  builder.Services.AddMcpServer(options =>
  {
      options.ServerInfo = new ServerInfo("qyl", "1.0.0");
      options.Capabilities.Tools = new ToolsCapability();
  });

  builder.Services.AddSingleton<QylCollectorClient>();
  builder.Services.AddToolsFromAssembly<TelemetryTools>();

  var host = builder.Build();
  await host.RunMcpServerAsync();
```

### 5. Dashboard Layer

```yaml
note: "VS-06 hat keine Dashboard Components (MCP ist CLI-basiert)"
```

## Tool Outputs (Markdown)

### query_spans Output

```markdown
## Spans (20 of 156 total)

| Time | Service | Name | Duration | Status |
|------|---------|------|----------|--------|
| 14:32:01 | api-gateway | POST /chat | 234ms | OK |
| 14:32:00 | openai-proxy | chat.completion | 198ms | OK |
| 14:31:58 | api-gateway | POST /chat | 1.2s | ERROR |

### GenAI Spans (3 of 20)

| Model | Tokens (in/out) | Cost |
|-------|-----------------|------|
| gpt-4o | 150/89 | $0.012 |
| gpt-4o | 200/156 | $0.018 |
```

### compare_sessions Output

```markdown
## Session Comparison

| Metric | Session A | Session B | Diff |
|--------|-----------|-----------|------|
| Total Spans | 45 | 62 | +37.8% |
| Error Rate | 2.2% | 8.1% | +5.9% |
| Avg Latency | 123ms | 456ms | +270.7% |
| Total Tokens | 5,234 | 12,456 | +138.0% |
| Estimated Cost | $0.42 | $1.12 | +166.7% |

### Key Differences

- Session B has significantly higher error rate
- Session B uses 2.4x more tokens
- Primary bottleneck: `openai.chat` (avg 890ms vs 120ms)
```

## Acceptance Criteria

- [ ] MCP Server startet via `dotnet run --project src/qyl.mcp`
- [ ] `query_spans` Tool funktioniert mit allen Filtern
- [ ] `get_session` Tool gibt Session mit GenAI Stats zurück
- [ ] `get_trace` Tool gibt ASCII Tree zurück
- [ ] `list_services` Tool listet alle Services
- [ ] `analyze_genai` Tool zeigt Usage und Kosten
- [ ] `compare_sessions` Tool vergleicht zwei Sessions
- [ ] Alle Outputs sind AI-freundlich formatiert (Markdown)
- [ ] Circuit Breaker schützt bei Collector-Ausfall
- [ ] Retry Logic bei temporären Fehlern

## Test Files

```yaml
unit_tests:
  - tests/qyl.mcp.tests/Client/QylCollectorClientTests.cs
  - tests/qyl.mcp.tests/Tools/QuerySpansToolTests.cs
  - tests/qyl.mcp.tests/Tools/CompareSessionsToolTests.cs
integration_tests:
  - tests/qyl.mcp.tests/McpServerIntegrationTests.cs
```

## Usage with Claude

```json
// claude_desktop_config.json
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/qyl/src/qyl.mcp"],
      "env": {
        "QYL_COLLECTOR_URL": "http://localhost:5100"
      }
    }
  }
}
```

**Example Prompts:**

```
User: "Show me the slowest requests from the last hour"
Claude: [calls query_spans with from=-1h, orderBy=duration]

User: "Why did session abc123 have so many errors?"
Claude: [calls get_session(abc123), analyzes error patterns]

User: "Compare yesterday's performance with today"
Claude: [calls compare_sessions with sessions from both days]
```

## Consequences

### Positive

- **AI-Native**: AI Agents können direkt mit Telemetrie arbeiten
- **Debugging**: Natural Language für komplexe Queries
- **Automation**: AI kann Reports generieren

### Negative

- **Latency**: HTTP roundtrip zu Collector + Processing
- **Limited Context**: MCP Tools haben Token-Limits für Outputs

### Risks

| Risk                       | Impact | Likelihood | Mitigation                   |
|----------------------------|--------|------------|------------------------------|
| Collector nicht erreichbar | High   | Medium     | Circuit Breaker + Retry      |
| Zu große Responses         | Medium | Medium     | Pagination + Truncation      |
| Sensitive Data Exposure    | High   | Low        | Redaction via qyl.compliance |

## References

- [ADR-0002](0002-vs01-span-ingestion.md) - VS-01 Span Ingestion
- [ADR-0003](0003-vs02-list-sessions.md) - VS-02 List Sessions
- [Model Context Protocol Spec](https://spec.modelcontextprotocol.io/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [qyl-architecture.yaml](../../qyl-architecture.yaml) Section: projects.qyl.mcp
