# MCP Server Specification

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

Data access via `IQylDataService` abstraction. MCP does NOT use ProjectReference to collector. It accesses data through HTTP or direct DI depending on deployment mode.

Reference: `docs/mcp-tool-audit.md` for the full 78-tool verification matrix.

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

### 2.6 Analysis Tools

- `AnalyzeTraceTool` — AI analysis of a trace
- `AnalyzeSessionTool` — AI analysis of a session
- `SuggestFixTool` — suggest fixes for errors

### 2.7 Triage Tools

- `AnnotateTraceTool` — mark traces with investigation notes
- `MarkTraceReviewedTool` — flag traces as reviewed

### 2.8 Management Tools

- `CreateProjectTool` — create new project
- `UpdateProjectTool` — update project settings
- `CreateApiKeyTool` — generate API keys
- `ConfigureRetentionTool` — set data retention policy

### 2.9 Other Tool Classes

ErrorTools, GenAiTools, FixTools, RcaTools, SummaryTools, ServiceTools, BuildTools, ClaudeCodeTools, CopilotTools, ConsoleTools, ReplayTools, SpanQueryTools, AnalyticsTools, AnomalyTools, AutofixMcpTools, AgentHandoffTools, GitHubMcpTools, RegressionTools, TriageTools, UseQylTools, ExportForAgentTools, InvestigateTools, AssistedQueryTools, StorageHealthTools, StructuredLogTools, TestGenerationTools, TelemetryTools.

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

Three modes:

1. **Embedded** — runs inside collector process. Direct DI access to services. No HTTP hop.
2. **Standalone stdio** — separate process, communicates via stdio. Token auth. Used with Claude Code.
3. **Standalone HTTP** — separate process, HTTP transport. OAuth via Keycloak. Used for remote MCP.

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

## 7. Agents

MCP server includes agent capabilities:

- `McpToolRegistry` — registers available MCP tools for agent use
- `HttpAgentProvider` — provides agents over HTTP
- `AgentLlmFactory` — creates LLM clients for agent tools

Prompt templates: `ObservabilitySystemPrompt`, `UseQylSystemPrompt`, `ErrorSummaryPrompt`, `FixGenPrompt`, `RcaPrompt`, `SessionSummaryPrompt`, `TraceSummaryPrompt`.

## 8. Known Gaps

- Responses lack citation fields, evidence references, and toolVersion
- Required before Anthropic Directory submission
- `QylDataException` error handling needs standardization across all tools

## 9. Definition of Done

- [ ] All 78 tools have readOnlyHint and destructiveHint annotations
- [ ] All list endpoints paginate (no unbounded results)
- [ ] All responses under 25,000 tokens
- [ ] Facts and analysis structurally separated in responses
- [ ] Three deployment modes work: embedded, stdio, HTTP
- [ ] Skills-based authorization restricts tools by level
- [ ] Interactive apps render correctly in Claude Desktop
- [ ] Entity IDs consistent across all tools for chaining
