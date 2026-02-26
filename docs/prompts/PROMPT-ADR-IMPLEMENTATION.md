# Prompt: Implement All ADRs — qyl Observability Platform

## You Are

Claude Opus 4.6, team lead. You orchestrate implementation of 5 ADRs for the qyl AI observability platform using the **Team feature** with Sonnet teammates. You do NOT implement everything yourself — you decompose, delegate, verify.

## Your Tools

- **Team feature**: `TeamCreate` → `TaskCreate` → spawn Sonnet teammates via `Task` tool → `SendMessage` to coordinate
- **Claude in Chrome** (MCP): browser automation routed to **Microsoft Edge** (not Chrome). Use `mcp__claude-in-chrome__*` tools for visual verification
- **All standard tools**: Bash, Read, Write, Edit, Grep, Glob for your own work

## The 5 ADRs to Implement

Read these files before starting — they are the source of truth:

| ADR | File | Summary |
|-----|------|---------|
| ADR-001 | `docs/adrs/ADR-001-docker-first-distribution.md` | Single Docker image, polyglot OTLP collector |
| ADR-002 | `docs/adrs/ADR-002-github-oauth-onboarding.md` | GitHub OAuth onboarding flow |
| ADR-003 | `docs/adrs/ADR-003-nuget-first-instrumentation.md` | .NET Premium SDK (qyl.servicedefaults) |
| ADR-004 | `docs/adrs/ADR-004-remove-qyl-cli.md` | Remove qyl.cli (ALREADY DONE — verify only) |
| ADR-005 | `docs/adrs/ADR-005-agent-framework-copilot.md` | Microsoft Agent Framework for qyl.copilot |

Additionally, implement the Agents Dashboard from: `PROMPT-AGENTS-DASHBOARD.md`

## Project Context

```text
Working directory: /Users/ancplua/qyl
Solution: qyl.slnx
Runtime: .NET 10.0 LTS, C# 14
Frontend: React 19, Vite 7, Tailwind CSS 4, shadcn/ui
Storage: DuckDB (columnar, glibc — no Alpine)
Protocol: OTel Semantic Conventions 1.39
Testing: xUnit v3, Microsoft Testing Platform
Ports: 5100 (HTTP/Dashboard), 4317 (gRPC OTLP), 5173 (Vite dev)
```

### Key Architecture Rules

- `qyl.protocol` is BCL-only — NO NuGet packages allowed
- `qyl.mcp` communicates with collector via HTTP only — NO ProjectReference to collector
- Generated files (`*.g.cs`, `api.ts`, `semconv.ts`) — NEVER edit manually
- TypeSpec is source of truth for API types: `core/specs/*.tsp`
- Use `TimeProvider.System.GetUtcNow()` — NEVER `DateTime.Now/UtcNow`
- Use `Lock _lock = new()` — NEVER `object _lock`
- Use `System.Text.Json` — NEVER `Newtonsoft.Json`
- Central Package Management: versions in `Directory.Packages.props`, NO `Version=` in csproj files
- Version variables in `Version.props`: `<PackageName>Version` pattern

### Existing Infrastructure

The following already exists and works:

- OTLP gRPC ingestion on :4317 (traces, logs, metrics)
- OTLP HTTP ingestion on :5100/v1/traces
- DuckDB storage (spans, logs, session_entities, errors tables)
- SSE live streaming via /api/v1/live
- Dashboard with routes: /traces, /genai, /agents, /logs, /sessions, /resources, /settings
- Semconv pipeline: `eng/semconv/generate-semconv.ts` → 5 outputs (TS, C#, C# UTF-8, TypeSpec, DuckDB SQL)
- NUKE build system: `nuke Full`, `nuke Generate`, `nuke Test`
- Docker multi-stage build: `src/qyl.collector/Dockerfile`
- qyl.copilot with GitHub Copilot SSE/AG-UI integration
- qyl.servicedefaults with source generators (TracedInterceptorEmitter, GenAiAttributes, etc.)

## Team Composition

Create a team named `adr-implementation`. Spawn these Sonnet teammates:

| Name | subagent_type | Responsibility |
|------|---------------|----------------|
| `docker-engineer` | `general-purpose` | ADR-001: Dockerfile optimization, health checks, port config, compose file |
| `auth-engineer` | `general-purpose` | ADR-002: GitHub OAuth flow, token storage, onboarding UI |
| `sdk-engineer` | `general-purpose` | ADR-003: qyl.servicedefaults source generators, auto-detection |
| `copilot-engineer` | `general-purpose` | ADR-005: Microsoft Agent Framework integration in qyl.copilot |
| `dashboard-engineer` | `general-purpose` | Agents Dashboard: all 6 overview panels, trace list, trace detail slide-in, Models tab, Tools tab |
| `backend-engineer` | `general-purpose` | Agents Dashboard backend: 9 new API endpoints for agents analytics |
| `verifier` | `general-purpose` | Runs acceptance criteria from each ADR, visual verification via browser |

## Execution Strategy

### Phase 1: Parallel Foundation (Lane 1)

These have NO dependencies — launch all simultaneously:

1. **docker-engineer**: ADR-001 implementation
   - Verify existing Dockerfile serves dashboard at :5100 and OTLP at :4317
   - Add docker-compose.yaml to `eng/` (if not already complete)
   - Ensure health endpoints work: `/health`, `/health/live`, `/health/ready`
   - Verify SPA fallback serves dashboard on `GET /`
   - Test: `docker build -f src/qyl.collector/Dockerfile -t qyl .` succeeds

2. **sdk-engineer**: ADR-003 verification
   - Verify qyl.servicedefaults source generators auto-detect: HttpClient, Microsoft.Extensions.AI, EF Core
   - Verify MSBuild properties (`<QylGenAi>`, `<QylDatabase>`, etc.) control generation
   - Verify "without NuGet" path: standard OTel env var works for telemetry
   - Run: `dotnet build src/qyl.servicedefaults` succeeds

3. **backend-engineer**: Agents Dashboard API endpoints
   - Read `PROMPT-AGENTS-DASHBOARD.md` for exact specifications
   - Implement 9 endpoints in `src/qyl.collector/`:
     - `GET /api/v1/agents/overview/traffic`
     - `GET /api/v1/agents/overview/duration`
     - `GET /api/v1/agents/overview/issues`
     - `GET /api/v1/agents/overview/llm-calls`
     - `GET /api/v1/agents/overview/tokens`
     - `GET /api/v1/agents/overview/tool-calls`
     - `GET /api/v1/agents/traces`
     - `GET /api/v1/agents/models`
     - `GET /api/v1/agents/tools`
   - Time bucketing: auto-detect from range (< 24h → 1h, < 7d → 6h, < 30d → 1d, else → 1w)
   - Common params: `from`, `to`, `project`, `env`, `search`
   - Verify data shape by querying DuckDB: `SELECT attributes FROM spans LIMIT 1`

4. **ADR-004 verification** (you, the lead, do this directly — it's already done):
   - Confirm `src/qyl.cli/` doesn't exist
   - Confirm `qyl.slnx` has no qyl.cli reference
   - Confirm CLAUDE.md has no qyl.cli mention
   - Mark ADR-004 status as "Accepted"

### Phase 2: Dependent Work (Lane 2)

Starts after Lane 1 completes:

5. **auth-engineer**: ADR-002 GitHub OAuth
   - Depends on: docker-engineer (container needs to serve onboarding page)
   - Implement GitHub Device Flow (for CLI) + Web Flow (for browser)
   - Token storage in container (ephemeral) or volume mount (persistent)
   - `QYL_GITHUB_TOKEN` env var for CI/headless
   - Onboarding screen at `/` when no token present
   - `GET /api/v1/github/repos` endpoint
   - OTLP ingestion MUST work without auth (telemetry has no auth gate)

6. **dashboard-engineer**: Agents Dashboard frontend
   - Depends on: backend-engineer (needs API endpoints to exist)
   - Read `PROMPT-AGENTS-DASHBOARD.md` for pixel-level specifications
   - Implement in `src/qyl.dashboard/src/pages/` and `src/qyl.dashboard/src/components/`
   - 6 overview panels (Traffic, Duration, Issues, LLM Calls, Tokens, Tool Calls)
   - Trace list table with all 9 columns
   - Abbreviated trace view (60% slide-in panel with AI span waterfall)
   - Models tab and Tools tab
   - Dark theme, purple accent, professional typography
   - Use semconv.ts constants — never hardcode attribute strings
   - Use TanStack Query with filter-aware cache keys
   - Loading states: skeleton placeholders, not spinners

### Phase 3: Integration (Lane 3)

7. **copilot-engineer**: ADR-005 Microsoft Agent Framework
   - Depends on: auth-engineer (copilot needs GitHub token for tools)
   - Adopt Microsoft Agent Framework in qyl.copilot
   - Support providers: Ollama (default, local, free), OpenAI, Anthropic, any OpenAI-compatible
   - Environment variables: `QYL_LLM_PROVIDER`, `QYL_LLM_ENDPOINT`, `QYL_LLM_MODEL`, `QYL_LLM_API_KEY`
   - Without LLM: all telemetry features work, agent features show "Configure LLM to enable"
   - MCP tools integration (24 tools from qyl.mcp)

### Phase 4: Verification (Lane 4)

8. **verifier**: End-to-end verification of ALL ADRs
   - Uses Claude in Chrome (Edge) for visual verification
   - Runs each ADR's "Verification Steps (Agent-Executable)" section
   - ADR-001: Docker build → run → health check → OTLP span → dashboard visible
   - ADR-002: No token → onboarding screen. With token → repos endpoint works
   - ADR-003: Build with servicedefaults → interceptors emitted. Build without → clean
   - ADR-004: Grep qyl.cli → zero matches (except CHANGELOG)
   - ADR-005: Chat endpoint with Ollama → agent responds with MCP tool results
   - Dashboard: Navigate to /agents → all 6 panels render → click trace → slide-in works

## Verification Protocol (for the verifier teammate)

### Browser Verification Steps

Two browser automation options available — use whichever works:

**Option A: Playwright MCP** (`mcp__playwright`) — configured for Edge (`--browser msedge`):
```
1. mcp__playwright browser_navigate url="http://localhost:5100"
2. mcp__playwright browser_screenshot
3. mcp__playwright browser_click element="Agents link in sidebar"
4. mcp__playwright browser_screenshot → verify 6 panels render
5. mcp__playwright browser_click element="first trace ID in table"
6. mcp__playwright browser_screenshot → verify slide-in panel
```

**Option B: Claude in Chrome** (`mcp__claude-in-chrome__*`) — if Edge extension installed:
```
1. tabs_context_mcp → get tab context
2. tabs_create_mcp → new tab
3. navigate to http://localhost:5100
4. screenshot → verify dashboard loads
5. find "Agents" link in sidebar → click
6. screenshot → verify 6 panels render
7. Wait for data to load (charts populated)
8. screenshot → verify charts have data
9. Click a trace ID → verify slide-in panel appears
10. screenshot → verify waterfall + span detail
```

### Backend Verification

```bash
# Health
curl -sf http://localhost:5100/health | jq .

# OTLP HTTP
curl -X POST http://localhost:5100/v1/traces \
  -H 'Content-Type: application/json' \
  -d '{"resourceSpans":[{"resource":{"attributes":[]},"scopeSpans":[{"scope":{"name":"test"},"spans":[{"traceId":"0af7651916cd43dd8448eb211c80319c","spanId":"b7ad6b7169203331","name":"test-span","kind":1,"startTimeUnixNano":"1704067200000000000","endTimeUnixNano":"1704067201000000000","status":{}}]}]}]}'

# Agents Overview
curl -sf http://localhost:5100/api/v1/agents/overview/traffic?from=0&to=9999999999999 | jq .
curl -sf http://localhost:5100/api/v1/agents/traces?from=0&to=9999999999999 | jq .

# GitHub (if token set)
curl -sf http://localhost:5100/api/v1/github/repos | jq .
```

### Build Verification

```bash
dotnet build qyl.slnx
dotnet test
nuke Full
```

## Coordination Rules

1. **You (team lead) do NOT write implementation code** — you decompose, delegate, review, and verify
2. **Each teammate works in isolation** — no two teammates edit the same file
3. **If a teammate is blocked**, they message you. You unblock or reassign.
4. **After each phase**, run `dotnet build qyl.slnx` to verify no compilation errors
5. **After all phases**, run `dotnet test` to verify no test regressions
6. **Mark ADR status** from "Proposed" to "Accepted" as each is verified

## File Ownership (prevent conflicts)

| Teammate | Owns |
|----------|------|
| docker-engineer | `src/qyl.collector/Dockerfile`, `eng/compose.yaml`, health endpoint files |
| auth-engineer | `src/qyl.collector/Auth/`, `src/qyl.collector/Identity/`, onboarding dashboard components |
| sdk-engineer | `src/qyl.servicedefaults/`, `src/qyl.servicedefaults.generator/` |
| copilot-engineer | `src/qyl.copilot/` |
| backend-engineer | `src/qyl.collector/Analytics/`, `src/qyl.collector/AgentRuns/`, new endpoint files |
| dashboard-engineer | `src/qyl.dashboard/src/pages/Agents*`, `src/qyl.dashboard/src/components/agents/` |
| verifier | No file ownership — read-only verification + browser automation |

## Success Criteria

When ALL of these are true, the implementation is complete:

- [ ] `docker build -f src/qyl.collector/Dockerfile -t qyl .` succeeds
- [ ] `docker run -p 5100:5100 -p 4317:4317 qyl` → health 200 within 10s
- [ ] OTLP span from any language appears in dashboard
- [ ] `/agents` page shows 6 panels with real DuckDB data
- [ ] Trace list table shows correct columns
- [ ] Click trace → abbreviated trace view with AI span waterfall
- [ ] No auth → onboarding screen. With token → repos endpoint works
- [ ] `QYL_LLM_PROVIDER=ollama` → copilot agent responds to chat
- [ ] No LLM configured → telemetry works, agent disabled with message
- [ ] `dotnet build qyl.slnx` → 0 errors, 0 warnings
- [ ] `dotnet test` → all tests pass
- [ ] No `qyl.cli` references (except CHANGELOG)
- [ ] All 5 ADRs status updated to "Accepted"

## Anti-Patterns

- Do NOT implement everything in one context window — use Team feature for parallel work
- Do NOT hardcode OTel attribute strings — use `semconv.ts` (frontend) and `SemanticConventions.g.cs` (backend)
- Do NOT add `Version=` to any csproj PackageReference — use Central Package Management
- Do NOT use `DateTime.Now` — use `TimeProvider.System.GetUtcNow()`
- Do NOT edit generated files (`*.g.cs`, `api.ts`, `semconv.ts`)
- Do NOT add new NuGet packages without first checking latest stable version via `https://api.nuget.org/v3-flatcontainer/{id}/index.json`
- Do NOT skip verification — every ADR has "Verification Steps (Agent-Executable)"
- Do NOT send OTLP to the wrong port — gRPC is :4317, HTTP JSON is :5100/v1/traces

## Start

1. Read all 5 ADR files + `PROMPT-AGENTS-DASHBOARD.md`
2. `TeamCreate` with name `adr-implementation`
3. `TaskCreate` for each work item (with dependencies)
4. Spawn Phase 1 teammates (all parallel)
5. Coordinate, unblock, review
6. Proceed through phases
7. Final verification with browser automation
