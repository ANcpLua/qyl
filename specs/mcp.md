# MCP Server Specification

> Owner: mcp
> SSOT: YES (MCP tool surface, skills/auth, deployment modes, tool contract)
> Depends on: `api.md` (response contract), `00-architecture.md` (ownership boundaries)
> Used by: `telemetry-intelligence.md` (intelligence tool surface)

Model Context Protocol server exposing qyl telemetry to LLM workflows. 78 tools across 27 classes.

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

- [ ] All tools have readOnlyHint and destructiveHint annotations
- [ ] All tools declare inputSchema and outputSchema
- [ ] All tools include toolVersion
- [ ] All list tools paginate with cursor (no unbounded results)
- [ ] All responses under 25,000 tokens
- [ ] Facts and analysis structurally separated in responses
- [ ] Both deployment modes work: stdio, streamable HTTP
- [ ] Skills-based authorization restricts tools by level
- [ ] Interactive apps render correctly in Claude Desktop
- [ ] Entity IDs consistent across all tools for chaining
- [ ] All tools use standardized error codes
- [ ] Evidence/citation references included in analysis responses
