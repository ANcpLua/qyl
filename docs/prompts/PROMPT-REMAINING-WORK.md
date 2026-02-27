# Prompt: Remaining Work — qyl Observability Platform

## What's Done (do NOT re-implement)

| Item | Evidence |
|------|----------|
| ADR-001 Docker-first | Dockerfile, compose, health endpoints, SPA fallback all exist |
| ADR-002 GitHub OAuth | Status: Done. GitHubService.cs, GitHubEndpoints.cs, OnboardingPage.tsx |
| ADR-003 ServiceDefaults | 6 analyzers, 5 emitters, MSBuild toggles, ProviderRegistry (15 GenAI providers) |
| ADR-004 Remove CLI | src/qyl.cli/ deleted, no references remain |
| ADR-005 LLM Providers | LlmProviderFactory.cs: Ollama/OpenAI/Anthropic/openai-compatible, env vars wired |
| Agents Dashboard backend | AgentInsightsEndpoints.cs (10 endpoints), AgentInsightsService.cs (834 lines DuckDB queries) |
| Agents Dashboard frontend | AgentsPage.tsx (1,151 lines), use-agent-insights.ts (282 lines), AgentTraceTree.tsx |

## What's Left (3 items)

### 1. Browser → Server Trace Correlation (5 files)

Per-interaction traces + `session.id` resource attribute. NOT one mega-trace.

**context.ts** — Add after line 27:
```ts
let sessionId: string;
export function initSessionContext(): void { sessionId = generateTraceId(); }
export function getSessionId(): string { return sessionId; }
```
No change to patchFetch — each fetch already gets its own trace.

**core.ts** — Import `initSessionContext`, `getSessionId`. Call `initSessionContext()` after sampling check. Pass `getSessionId()` to `new Transport(resolved, getSessionId())`.

**transport.ts** — Constructor: `constructor(config: ResolvedConfig, private sessionId: string)`. Add `{key: 'session.id', value: {stringValue: this.sessionId}}` to resource attributes.

**errors.ts** — Import `generateSpanId`, `generateTraceId`. In `errorToLog`, add `traceId: generateTraceId()` and `spanId: generateSpanId()` to returned object.

**Program.cs** — Two one-liners:
- Line 121: `AllowedOrigins = builder.Configuration["QYL_OTLP_CORS_ALLOWED_ORIGINS"] ?? "*",`
- Line 99: Add `"/v1/logs"` to `ExcludedPaths` array

**Verify**: `cd src/qyl.browser && npm run build` + `dotnet build src/qyl.collector`

### 2. ADR-005 Copilot End-to-End Verification

LlmProviderFactory exists but needs integration testing:

- [ ] No LLM configured → telemetry works, copilot returns "Configure LLM to enable"
- [ ] `QYL_LLM_PROVIDER=ollama` → copilot agent responds to chat via `/v1/chat/completions`
- [ ] MCP tools integration: copilot can call qyl.mcp tools (search_spans, get_trace, etc.)
- [ ] Streaming works via `IAsyncEnumerable`

If any of these fail, fix the integration. The factory code is done — the wiring may not be.

### 3. Visual Verification (browser automation)

Use Playwright MCP (`mcp__playwright`, `--browser msedge`) or Claude in Chrome (`mcp__claude-in-chrome__*`):

**Agents Dashboard**:
```
1. Navigate to http://localhost:5100
2. Click "Agents" in sidebar
3. Screenshot → verify 6 overview panels render (Traffic, Duration, Issues, LLM Calls, Tokens, Tool Calls)
4. Verify chart styling: dark theme, purple accent, no gridlines, compact notation ("355m", "$0.0151")
5. Scroll to trace list table → verify 9 columns
6. Click a trace ID → verify 60% slide-in panel with AI span waterfall
7. Click Models tab → verify model analytics table
8. Click Tools tab → verify tool usage table
```

**Onboarding (ADR-002)**:
```
1. Start collector without QYL_GITHUB_TOKEN
2. Navigate to http://localhost:5100
3. Screenshot → verify onboarding screen ("Connect GitHub to get started")
4. Set QYL_GITHUB_TOKEN, restart
5. Navigate → verify full dashboard loads
```

**Health + OTLP**:
```bash
curl -sf http://localhost:5100/health | jq .
curl -X POST http://localhost:5100/v1/traces \
  -H 'Content-Type: application/json' \
  -d '{"resourceSpans":[{"resource":{"attributes":[]},"scopeSpans":[{"scope":{"name":"test"},"spans":[{"traceId":"0af7651916cd43dd8448eb211c80319c","spanId":"b7ad6b7169203331","name":"test-span","kind":1,"startTimeUnixNano":"1704067200000000000","endTimeUnixNano":"1704067201000000000","status":{}}]}]}]}'
curl -sf http://localhost:5100/api/v1/agents/overview/traffic?from=0&to=9999999999999 | jq .
```

**Build**:
```bash
dotnet build qyl.slnx    # 0 errors
dotnet test               # all pass
nuke Full                 # complete pipeline
```

## Project Context

```text
Working directory: /Users/ancplua/qyl
Runtime: .NET 10.0 LTS, C# 14
Frontend: React 19, Vite 7, Tailwind CSS 4
Storage: DuckDB (glibc, no Alpine)
Protocol: OTel Semantic Conventions 1.40
Ports: 5100 (HTTP), 4317 (gRPC OTLP), 5173 (Vite dev)
```

## Rules

- `TimeProvider.System.GetUtcNow()` not `DateTime.Now`
- `Lock _lock = new()` not `object _lock`
- `System.Text.Json` not Newtonsoft
- Central Package Management: no `Version=` in csproj
- Never edit `*.g.cs`, `api.ts`, `semconv.ts`
- Use `semconv.ts` constants in frontend, `GenAiAttributes` in backend
