# MCP Tool Audit — Phase 4 Verification Matrix

**Date:** 2026-03-10
**Commit:** 7d39137 (main)
**Method:** Read every `[McpServerTool]` method in `src/qyl.mcp/Tools/`, cross-referenced against `MapGet`/`MapPost`/
`MapPut`/`MapPatch`/`MapDelete` in `src/qyl.collector/`.

## Summary

| Metric                            | Count |
|-----------------------------------|-------|
| Tool classes                      | 27    |
| Total `[McpServerTool]` methods   | 78    |
| OK (endpoint exists)              | 67    |
| BROKEN (endpoint missing)         | 3     |
| INDIRECT (via HttpTelemetryStore) | 6     |
| LOCAL (no HTTP, embedded LLM)     | 2     |

## Verification Matrix

| #  | Tool Class          | Methods | Endpoints Called                                                                                                                                       | Status             |
|----|---------------------|---------|--------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------|
| 1  | ReplayTools         | 4       | `/api/v1/sessions`, `/api/v1/sessions/{id}/spans`, `/api/v1/traces/{traceId}`                                                                          | OK                 |
| 2  | ConsoleTools        | 2       | `/api/v1/console`, `/api/v1/console/errors`                                                                                                            | OK                 |
| 3  | StructuredLogTools  | 3       | `/api/v1/logs` (with query params)                                                                                                                     | OK                 |
| 4  | GenAiTools          | 4       | `/api/v1/genai/stats`, `/api/v1/genai/spans` (OK); `/api/v1/genai/models`, `/api/v1/genai/usage/timeseries` (**BROKEN**)                               | **2 OK, 2 BROKEN** |
| 5  | ErrorTools          | 4       | `/api/v1/issues`, `/api/v1/issues/{id}`, `/api/v1/issues/{id}/events`, `/api/v1/issues/similar`, `/api/v1/issues/{id}/timeline`                        | OK                 |
| 6  | ServiceTools        | 1       | `/api/v1/services`                                                                                                                                     | OK                 |
| 7  | SpanQueryTools      | 1       | `/api/v1/sessions/{id}/spans` or `/api/v1/genai/spans`                                                                                                 | OK                 |
| 8  | StorageHealthTools  | 3       | `/health`, `/alive`, `/ready`, `/api/v1/insights`                                                                                                      | OK                 |
| 9  | AnalyticsTools      | 8       | `/api/v1/analytics/conversations`, `…/{id}`, `/coverage-gaps`, `/top-questions`, `/source-analytics`, `/satisfaction`, `/users`, `/users/{id}/journey` | OK                 |
| 10 | AnomalyTools        | 3       | `/api/v1/analytics/anomaly/anomalies`, `/baseline`, `/compare`                                                                                         | OK                 |
| 11 | BuildTools          | 3       | `/api/v1/build-failures`, `/{id}`, `/search`                                                                                                           | OK                 |
| 12 | CopilotTools        | 3       | `/api/v1/copilot/status`, `/chat`, `/workflows/{name}/run`                                                                                             | OK                 |
| 13 | ClaudeCodeTools     | 5       | `/api/v1/claude-code/sessions`, `/{id}/timeline`, `/{id}/tools`, `/attach` (POST+DELETE)                                                               | OK                 |
| 14 | TelemetryTools      | 5       | Via `HttpTelemetryStore` → `/api/v1/sessions`                                                                                                          | INDIRECT           |
| 15 | InvestigateTools    | 1       | Via `IAgentProvider` → collector agent endpoint                                                                                                        | INDIRECT           |
| 16 | RcaTools            | 1       | No direct HTTP — embedded LLM agent orchestrating ErrorTools, AnomalyTools, SpanQueryTools, StructuredLogTools                                         | LOCAL              |
| 17 | UseQylTools         | 1       | No direct HTTP — embedded LLM meta-agent with `McpToolRegistry`                                                                                        | LOCAL              |
| 18 | TriageTools         | 3       | `/api/v1/issues/{id}/triage` (GET+POST), `/api/v1/triage`                                                                                              | OK                 |
| 19 | ExportForAgentTools | 1       | `/api/v1/issues/{id}`, `/{id}/events`, `/{id}/triage`, `/{id}/fix-runs`                                                                                | OK                 |
| 20 | FixTools            | 1       | POST `/api/v1/issues/{id}/fix-runs`, PATCH `/{id}/fix-runs/{runId}` + embedded RCA agent                                                               | OK                 |
| 21 | AutofixMcpTools     | 5       | `/api/v1/issues/{id}/fix-runs` (GET), `/{runId}` (GET), `/{runId}/steps`, `/{runId}/approve`, `/{runId}/reject`                                        | OK                 |
| 22 | RegressionTools     | 3       | POST `/api/v1/regressions/check/{serviceName}`, GET `/api/v1/regressions`, GET `/api/v1/issues/{id}/regressions`                                       | OK                 |
| 23 | GitHubMcpTools      | 3       | `/api/v1/code-review/{repo}/pulls/{pr}` (GET+POST), `/api/v1/github/events`                                                                            | OK                 |
| 24 | AgentHandoffTools   | 5       | `/api/v1/handoffs/pending`, `/{id}/context`, `/{id}/accept`, `/{id}/submit`, `/{id}/fail`                                                              | OK                 |
| 25 | AssistedQueryTools  | 1       | POST `/api/v1/query` + embedded LLM for SQL generation                                                                                                 | OK                 |
| 26 | TestGenerationTools | 1       | GET `/api/v1/errors/{id}` (OK), GET `/api/v1/errors/{id}/events` (**BROKEN**)                                                                          | **BROKEN**         |
| 27 | SummaryTools        | 3       | `/api/v1/issues/{id}`, `/{id}/events`, `/api/v1/traces/{id}`, `/api/v1/sessions/{id}`, `/{id}/spans` + embedded LLM                                    | OK                 |

## Broken Tools — Details

### 1. `qyl.list_models` (GenAiTools.cs:152)

- **Calls:** `GET /api/v1/genai/models?hours={hours}`
- **Collector has:** `/api/v1/genai/stats`, `/api/v1/genai/spans` — no `/genai/models`
- **Nearest match:** `/api/v1/agents/models` (AgentInsightsEndpoints.cs) — different route group
- **Fix:** Add `MapGet("/api/v1/genai/models", ...)` to collector, or retarget tool to `/api/v1/agents/models`

### 2. `qyl.get_token_timeseries` (GenAiTools.cs:196)

- **Calls:** `GET /api/v1/genai/usage/timeseries?hours={hours}&interval={interval}`
- **Collector has:** No timeseries endpoint under `/api/v1/genai/`
- **Fix:** Add `MapGet("/api/v1/genai/usage/timeseries", ...)` to collector with hourly/daily token aggregation query

### 3. `qyl.generate_test_from_error` (TestGenerationTools.cs:27)

- **Calls:** `GET /api/v1/errors/{issueId}/events?limit=3`
- **Collector has:** `/api/v1/errors/{errorId}` (GET), `/api/v1/errors/stats`, `/api/v1/errors/{errorId}` (PATCH) — no
  `/errors/{id}/events`
- **Correct path:** `/api/v1/issues/{issueId}/events` (IssueEndpoints.cs:112)
- **Fix:** Change the URL in TestGenerationTools.cs from `/api/v1/errors/` to `/api/v1/issues/`

## Smoke Test

**Command:**

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{...}}' | dotnet run --project src/qyl.mcp/qyl.mcp.csproj
```

**Result: FAILED — startup crash**

```
System.NotSupportedException: JsonTypeInfo metadata for type
'qyl.mcp.Tools.ToolResult`1[qyl.mcp.Tools.AgentRun[]]' was not provided by
TypeInfoResolver of type '[TelemetryJsonContext, ConsoleJsonContext, ...]'
```

**Root cause:** `TelemetryTools` returns `ToolResult<AgentRun[]>`, `ToolResult<AgentRun?>`,
`ToolResult<TokenUsageSummary[]>`, `ToolResult<AgentError[]>`, `ToolResult<LatencyStats>`. These generic types are not
registered in any `JsonSerializerContext`, so the MCP SDK's `AIFunctionFactory` fails when building JSON schemas for
tool parameters.

**Impact:** The MCP server cannot start at all — no tools are available via stdio.

**Fix:** Either:

1. Add `[JsonSerializable(typeof(ToolResult<AgentRun[]>))]` etc. to `TelemetryJsonContext`, or
2. Change `TelemetryTools` to return `Task<string>` like all other tool classes (preferred — matches the codebase
   pattern)

## Deployment Status

| Component             | Status             | Details                                                                                                               |
|-----------------------|--------------------|-----------------------------------------------------------------------------------------------------------------------|
| **mcp.qyl.info**      | Collector instance | Railway deployment. Serves dashboard at `/`, REST API at `/api/v1/*`, health at `/health`. **Not an MCP SSE server.** |
| **Collector health**  | Healthy            | 129K+ spans, 5K+ sessions, ~5 days uptime                                                                             |
| **MCP server**        | stdio only         | `src/qyl.mcp/` — no SSE transport. Cannot be accessed remotely.                                                       |
| **Remote MCP access** | Not deployed       | Would require `WithSseServerTransport()` in collector or deploying MCP as a separate service                          |

### .mcp.json Configuration

For local development, use this `.mcp.json` in the repo root (gitignored):

```json
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "src/qyl.mcp/qyl.mcp.csproj"],
      "env": {
        "QYL_COLLECTOR_URL": "http://localhost:5100"
      }
    }
  }
}
```

**Note:** The MCP server currently fails to start due to the AOT serialization issue above. Fix the `ToolResult<T>`
types first.

## Infrastructure Files (not tool classes)

| File                      | Purpose                                                                 |
|---------------------------|-------------------------------------------------------------------------|
| `CollectorHelper.cs`      | Error-handling wrapper for HTTP calls                                   |
| `HttpTelemetryStore.cs`   | `ITelemetryStore` impl routing all 5 methods through `/api/v1/sessions` |
| `TelemetryJsonContext.cs` | Combined AOT serializer context (17 sub-contexts)                       |
