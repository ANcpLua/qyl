# MCP Server Specification

> Owner: mcp
> SSOT: YES (MCP tool surface, skills/auth, deployment modes, tool contract)
> Depends on: `api.md` (response contract), `00-architecture.md` (ownership boundaries)
> Used by: `telemetry-intelligence.md` (intelligence tool surface)

Model Context Protocol server exposing qyl telemetry to LLM workflows. 118 tools across 49 classes.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Tool Surface](#2-tool-surface)
3. [Skills and Auth](#3-skills-and-auth)
4. [Deployment Modes](#4-deployment-modes)
5. [Response Format](#5-response-format)
6. [Apps](#6-apps)
7. [Agents](#7-agents)
8. [Known Gaps](#8-known-gaps)
9. [Definition of Done](#9-definition-of-done)

---

## 1. Overview

`src/qyl.mcp/` — ModelContextProtocol.AspNetCore 1.1.0 native.

Data access via `IQylDataService` abstraction over HTTP to `qyl.collector`. MCP does NOT use ProjectReference to collector. Separate process, HTTP only.

Reference: `docs/mcp-tool-audit.md` for the full tool verification matrix.

## 2. Tool Surface

### 2.1 Discovery Tools

- `ListProjectsTool` — list configured projects
- `ListServicesTool` — list discovered services
- `GetServiceMapTool` — service dependency graph

### 2.2 Trace Tools

- `SearchTracesTool` — query traces by attributes, time range
- `GetTraceDetailsTool` — full trace with all spans
- `GetSpanTool` — single span detail

### 2.3 Log Tools

- `SearchLogsTool` — query structured logs

### 2.4 Metric Tools

- `ListMetricsTool` — list available metrics
- `QueryMetricsTool` — query metric time series

### 2.5 Session Tools

- `SearchSessionsTool` — find sessions by criteria
- `GetSessionTool` — session detail
- `AnnotateSessionTool` — add notes to sessions
- `UpdateSessionStatusTool` — change session state

### 2.6 Analysis Tools (delegate to Loom)

- `AnalyzeTraceTool` — AI analysis of a trace (delegates to Loom API)
- `AnalyzeSessionTool` — AI analysis of a session (delegates to Loom API)
- `SuggestFixTool` — suggest fixes for errors (delegates to Loom API)

MCP does NOT perform AI analysis locally. It proxies analysis requests to Loom's REST API. If Loom is unavailable, analysis tools return an error — they do not fall back to local computation.

### 2.7 Triage Tools

- `AnnotateTraceTool` — mark traces with investigation notes
- `MarkTraceReviewedTool` — flag traces as reviewed

### 2.8 Management Tools

- `CreateProjectTool` — create new project
- `UpdateProjectTool` — update project settings
- `CreateApiKeyTool` — generate API keys
- `ConfigureRetentionTool` — set data retention policy

### 2.9 Other Tool Classes

ErrorTools, GenAiTools, FixTools, RcaTools, SummaryTools, ServiceTools, ReplayTools, SpanQueryTools, AnalyticsTools, AnomalyTools, AutofixMcpTools, GitHubMcpTools, RegressionTools, TriageTools, UseQylTools, ExportForAgentTools, AssistedQueryTools, StorageHealthTools, StructuredLogTools, TestGenerationTools, TelemetryTools.

Deleted per MAF migration: `CopilotTools`, `ClaudeCodeTools`, `ConsoleTools`, `BuildTools`, `AgentHandoffTools`, `InvestigateTools` (HttpAgentProvider dependency removed).

## 3. Skills and Auth

### 3.1 Skills

`QylSkillKind` defines authorization levels:

- **inspect** — read-only telemetry access
- **triage** — annotate and triage capabilities
- **analyze** — AI-powered analysis tools

`SkillConfiguration` + `SkillRegistrationExtensions` register tools by skill level.

### 3.2 Auth

- `McpAuthHandler` — authentication handler
- `McpAuthOptions` — auth configuration
- `McpAuthExtensions` — DI registration
- `McpAdminToolFilter` — restricts admin tools
- `KeycloakTokenProvider` — OAuth token provider for remote deployment

### 3.3 Scoping

`QylScope` — per-request scope (project, time range, service filter).
`ScopingDelegatingHandler` — injects scope into outgoing HTTP calls.

## 4. Deployment Modes

Two modes:

1. **stdio** — local, separate process, communicates via stdio. Token auth. Used with Claude Code.
2. **Streamable HTTP** — remote, separate process, HTTP transport at `/mcp`. OAuth 2.1 via Keycloak. Used for Anthropic Directory / remote clients.

Both modes access the collector via HTTP. No embedded mode — MCP is always a separate process per `00-architecture.md` section 9 (forbidden dependencies).

Railway production note from 2026-04-02: the `qyl-api` service variables UI currently shows
`QYL_MCP_TRANSPORT=http` and `QYL_COLLECTOR_URL=http://localhost:5100`. That does **not** make `qyl-api`
the MCP deployment. Remote MCP remains a separate service (`qyl-mcp`) and must be verified by its own
public routes (`/`, `/mcp`, `/mcp.json`, `/healthz`) and Railway build config.

Monorepo deployment rule: `qyl-mcp` must use the dedicated config file at `/src/qyl.mcp/railway.toml`.
The repo-root `railway.toml` belongs to `qyl.collector`.

`McpHostOptions` configures the deployment mode.

## 5. Response Format

### 5.1 Directory Requirements

All tools must:

- Declare `readOnlyHint` and `destructiveHint` annotations
- Keep responses under 25,000 tokens
- Paginate list endpoints
- Return structured data (not markdown walls)
- Distinguish "no results" from "error"
- Never leak internal IDs, connection strings, or infrastructure details

### 5.2 Provenance

Responses must structurally separate:

- Raw telemetry facts (`facts` field)
- AI-generated analysis (`analysis` field)
- Proposed actions (`actions` field)

Never interleave facts and analysis in a single text blob.

### 5.3 Formatting

`ResponseFormatter` + `CollectorDtos` handle response shaping.
Timestamps in ISO 8601 UTC. Never relative ("5 minutes ago").
Consistent entity ID formats across all tools for tool-call chaining.

## 6. Apps

Interactive HTML apps served via MCP resources:

- `ErrorExplorer` — `error-explorer.html` + `ErrorExplorerTools`
- `QueryStudio` — `query-studio.html` + `QueryStudioTools`
- `TraceExplorer` — `trace-viewer.html` + `TraceExplorerTools`

These qualify for the Anthropic Directory "Interactive" badge.

## 7. Responsibility Boundary

Ownership boundaries: see `00-architecture.md` section 2.3.

MCP may delegate to Loom's API for analysis tools (section 2.6), but MCP itself has no LLM dependencies and performs no AI computation.

Prompt templates for MCP-local formatting: `ObservabilitySystemPrompt`, `UseQylSystemPrompt`, `ErrorSummaryPrompt`, `SessionSummaryPrompt`, `TraceSummaryPrompt`.

## 8. Tool Contract

Every tool must conform to this contract:

```text
Tool {
  name:             string          // kebab-case, unique
  toolVersion:      string          // semver, bumped on breaking changes
  readOnlyHint:     boolean         // safety annotation
  destructiveHint:  boolean         // safety annotation
  inputSchema:      JSON Schema     // strict, no additionalProperties
  outputSchema: {
    facts:          object          // raw telemetry data, never AI-generated
    analysis?:      object          // AI-generated (only if delegated to Loom)
    actions?:       object[]        // proposed follow-up actions
    pagination?: {
      cursor:       string | null   // opaque cursor for next page
      hasMore:      boolean
    }
    evidence?: {
      sources:      string[]        // entity IDs referenced
      timeRange:    { from, to }    // ISO 8601 UTC
    }
  }
  errorModel: {
    code:           string          // machine-readable error code
    message:        string          // human-readable description
    retryable:      boolean
  }
}
```

### 8.1 Pagination

All list tools use cursor-based pagination. Default page size: 50. Max: 200. No offset-based pagination, no `COUNT(*)`.

### 8.2 Error Handling

Standardized `QylDataException` with error codes:

| Code | Meaning |
|------|---------|
| `not_found` | Entity does not exist |
| `no_results` | Query returned empty (not an error) |
| `invalid_input` | Schema validation failed |
| `loom_unavailable` | Analysis tool cannot reach Loom |
| `rate_limited` | Too many requests |
| `internal` | Unexpected server error |

`no_results` returns 200 with empty `facts`, NOT an error.

## 9. Definition of Done

- [x] All tools have readOnlyHint and destructiveHint annotations (118 tools, verified by grep)
- [x] All tools declare inputSchema and outputSchema
- [ ] All tools include toolVersion (SDK lacks Version property — blocked, not implemented)
- [ ] All list tools paginate with cursor (list_services, list_metrics, list_error_issues, list_triage lack cursor — not implemented, tracked as future work)
- [ ] All responses under 25,000 tokens (no hard enforcement — implicit via page sizes only, not implemented)
- [x] Facts and analysis structurally separated in responses (StructuredResponse envelope)
- [x] Both deployment modes work: stdio, streamable HTTP
- [x] Skills-based authorization restricts tools by level
- [ ] Interactive apps render correctly in Claude Desktop (not verified)
- [ ] Entity IDs consistent across all tools for chaining (not audited)
- [x] All tools use standardized error codes
- [ ] Evidence/citation references included in analysis responses (not implemented)

## 10. MCP Native Generator Migration (2026-03-22)

### 10.1 Current -> target

| Current | Target | Decision |
|------|------|------|
| `[McpServerToolType]` | `[McpServer]` | mechanical rename on every tool/resource container |
| `[McpServerTool(...)]` | `[Tool(...)]` | preserve tool names exactly; only `ReadOnly` stays source-visible |
| `[McpServerResourceType]` + `[McpServerResource(...)]` | `[McpServer]` + `[Resource("uri")]` | convert all resources to attribute-discoverable forms |
| `app.MapMcp(path)` | `app.MapMcpServer<T>(path)` | generated dispatcher owns MCP surface |
| hand-built schemas, manifests, tool spans | generated schemas, manifest, tool spans, `SKILL.md`, `llms.txt` | runtime keeps transport/auth only |

Repo facts to preserve while migrating:

- `Version.props` pins `ModelContextProtocolVersion` to `1.1.0`.
- `src/qyl.mcp/Program.cs` owns `app.MapMcp(hostOptions.Path)`, manual `/mcp.json` and `/llms.txt`, JSON-RPC message spans, and manual tool-call spans.
- `src/qyl.mcp/Skills/SkillRegistrationExtensions.cs` manually composes the server with `WithTools<>` and `WithResources<>`.
- `src/qyl.mcp/Agents/McpToolRegistry.cs`, `src/qyl.mcp/Tools/FixTools.cs`, and `src/qyl.mcp/Tools/RcaTools.cs` reflect over `McpServerToolAttribute`.
- `src/qyl.mcp/Apps/QueryStudio/QueryStudioResource.cs` is the one non-attribute resource today; it uses `McpServerResource.Create(...)`.
- Current annotated surface is 48 tool classes plus 3 resource classes under `src/qyl.mcp/Tools/` and `src/qyl.mcp/Apps/`.

### 10.2 Impacted files/modules

- Versioning and package wiring:
  - `Version.props`
  - `Directory.Packages.props`
  - `src/qyl.mcp/qyl.mcp.csproj`
- Host/bootstrap:
  - `src/qyl.mcp/Program.cs`
  - `src/qyl.mcp/McpHostOptions.cs`
- Skill and server composition:
  - `src/qyl.mcp/Skills/SkillRegistrationExtensions.cs`
  - `src/qyl.mcp/Apps/ErrorExplorer/ErrorExplorerRegistration.cs`
  - `src/qyl.mcp/Apps/QueryStudio/QueryStudioRegistration.cs`
  - `src/qyl.mcp/Apps/TraceExplorer/TraceExplorerRegistration.cs`
  - new: `src/qyl.mcp/Servers/` for generator-visible server roots
- Metadata consumers:
  - `src/qyl.mcp/Agents/McpToolRegistry.cs`
  - `src/qyl.mcp/Tools/FixTools.cs`
  - `src/qyl.mcp/Tools/RcaTools.cs`
  - `src/qyl.mcp/Auth/McpAdminToolFilter.cs`
- Annotated tool/resource surface:
  - `src/qyl.mcp/Tools/**/*.cs` carrying `[McpServerToolType]` / `[McpServerTool(...)]`
  - `src/qyl.mcp/Apps/*/*Tools.cs`
  - `src/qyl.mcp/Apps/*/*Resource.cs`
- Packaged docs/artifacts:
  - `src/qyl.mcp/README.md`
  - `src/qyl.mcp/.mcp/server.json`
- Cleanup debt:
  - `src/qyl.mcp/Clear.cs`
- New module:
  - `src/qyl.mcp.generators/` for the qyl-native incremental generator and emitted catalog/dispatch/schema/artifact pipeline

### 10.3 Migration sequence

1. Add `src/qyl.mcp.generators/` and make it the single owner of MCP source generation.
   Use an incremental generator, equatable models, and generator-time diagnostics only. Emit:
   dispatch, JSON Schema, tool/resource catalogs, tool OTel wrappers, `SKILL.md`, and `llms.txt`.

2. Upgrade the package surface before touching runtime code.
   Bump `ModelContextProtocolVersion` in `Version.props` and wire the new generator into
   `src/qyl.mcp/qyl.mcp.csproj`. Do not keep old and new MCP attribute models side by side.

3. Convert the annotated source mechanically.
   Replace `[McpServerToolType]` with `[McpServer]`.
   Replace every `[McpServerTool(Name = "...", ...)]` with `[Tool("...")]`, adding
   `[Tool(ReadOnly = ToolHint.True)]` where the current tool is read-only.
   Preserve existing `[Description]` text verbatim for schema/doc generation.
   Convert all resources to `[Resource("uri")]`; `QueryStudioResource.cs` must stop using
   `McpServerResource.Create(...)` so the generator can see it.
   Do not convert `Agents/*.cs` prompt strings to MCP prompts; they are internal LLM prompts, not MCP prompts.

4. Replace manual composition with generated server roots.
   Introduce server root markers under `src/qyl.mcp/Servers/` and have `Program.cs` map them with
   `MapMcpServer<T>()`.
   `SkillRegistrationExtensions.cs` should stop registering tools directly and instead select which generated
   server roots are enabled for the current `QYL_SKILLS` value.
   Delete the three `*Registration.cs` app wrappers; they become pure metadata, not runtime glue.

5. Move metadata consumers off reflection.
   `McpToolRegistry.cs`, `FixTools.cs`, and `RcaTools.cs` should consume a generated tool catalog instead of
   `GetCustomAttribute<McpServerToolAttribute>()`.
   Keep `McpAdminToolFilter.cs` name-based; it is policy, not discovery.

6. Shrink `Program.cs` aggressively.
   Keep transport selection, auth, health checks, collector HTTP clients, and JSON-RPC ingress/egress spans.
   Delete manual tool-call span wrapping once the generator emits per-tool spans.
   Delete `CreateManifest(...)` and `CreateLlmsText(...)`; generated artifacts should serve those endpoints.

7. Delete the old path completely.
   Remove `src/qyl.mcp/Clear.cs`, any remaining `WithTools<>` / `WithResources<>` calls, and any compatibility shim
   that translates old attributes to new ones.

### 10.4 Delete vs wrap vs generate

- Delete:
  - old MCP attributes and all registration code built around them
  - `src/qyl.mcp/Apps/*/*Registration.cs`
  - `Program.cs` helpers `CreateManifest(...)` and `CreateLlmsText(...)`
  - `src/qyl.mcp/Clear.cs`
- Wrap:
  - existing tool methods and collector HTTP logic
  - existing auth and transport split
  - `McpAdminToolFilter` and `QYL_SKILLS` policy
- Generate:
  - dispatch and schema
  - tool/resource/prompt catalogs
  - per-tool OTel spans
  - `SKILL.md`, `llms.txt`, and manifest payloads

Opinionated rule: do not build an adapter layer for `McpServerTool*` or `McpServerResource*`. This is internal code.
One rewrite is cheaper than carrying two metadata models forever.

### 10.5 Compatibility stance

- Break source compatibility immediately. Old attributes, `WithTools<>`, `WithResources<>`, and `MapMcp(...)` should disappear in one pass.
- Preserve wire compatibility where clients care:
  - keep tool names exactly as they are today
  - keep resource URIs exactly as they are today
  - keep `/mcp`, `/mcp.json`, `/llms.txt`, `/healthz`, stdio vs streamable HTTP, and auth behavior
  - keep `QYL_SKILLS` gating and do not surface disabled tools in discovery
- Do not manufacture `[Prompt("name")]` usage unless qyl exposes real MCP prompts. Internal agent prompts stay internal.

### 10.6 Validation and tests

- Compile-time:
  - `dotnet build src/qyl.mcp/qyl.mcp.csproj`
  - `dotnet build src/qyl.mcp.generators/qyl.mcp.generators.csproj`
  - zero matches for `McpServerToolType`, `McpServerTool(`, `McpServerResourceType`, `McpServerResource(`, `MapMcp(`, `WithTools<`, and `WithResources<` under `src/qyl.mcp`
- Generator correctness:
  - snapshot tests for generated schema and manifests for representative surfaces:
    `search_traces`, `qyl.generate_fix`, `qyl.app.trace_viewer`
  - assert the generated catalog contains the current tool names and the three app resource URIs
  - assert `QueryStudioResource` is now generator-discovered, not special-cased
- Integration:
  - stdio smoke test: list tools, call one read-only tool, call one mutating tool
  - HTTP smoke test: `/mcp`, `/mcp.json`, `/llms.txt`, `/healthz`
  - skill gating test: disabled skills do not appear in discovery
  - admin filter test: blocked tool name still returns denied result
- Observability:
  - one tool-level span per tool call from generated code
  - existing transport-level JSON-RPC spans still present
- Packaging:
  - generated `SKILL.md` and `llms.txt` land in build/package output and remain version-aligned with
    `src/qyl.mcp/.mcp/server.json`
