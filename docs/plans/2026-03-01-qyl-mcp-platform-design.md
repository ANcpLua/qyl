# qyl.mcp Platform Design

> **Date:** 2026-03-01
> **Status:** Approved
> **Goal:** Transform qyl.mcp from a stdio-only MCP tool into a distributable observability platform with OAuth, HTTP transport, multi-backend support, and platform-specific connectors.

## Context

qyl.mcp currently runs as a stdio MCP server distributed via NuGet (`PackAsTool`). It talks to a single backend (`qyl.collector` via HTTP) and uses a simple API key for auth.

Sentry's MCP server demonstrates a mature distribution model: OAuth, Streamable HTTP transport, skill-based tool filtering, Cloudflare Workers deployment, and one-click installation across AI clients (Claude Code, Cursor, VS Code). We want the same distribution reach for qyl, but provider-agnostic — qyl is an observability intelligence layer that can connect to Sentry, Seq, Datadog, or its own native collector.

## Architecture: Monolith Split (Approach A)

Four new projects, clean separation between shared logic, transports, and backends.

```
qyl/src/
├── qyl.mcp/                         # EXISTING — stdio transport (NuGet McpServer)
│   └── Program.cs                    # Thin: registers stdio + references core
│
├── qyl.mcp.core/                     # NEW — Shared library (no transport)
│   ├── Backends/
│   │   ├── IObservabilityBackend.cs   # Provider-agnostic interface
│   │   ├── BackendRegistry.cs         # Resolves backends by name
│   │   └── QylNativeBackend.cs        # Existing qyl.collector HTTP calls
│   ├── Tools/
│   │   ├── IdentityTools.cs           # whoami (multi-backend)
│   │   ├── IssueTools.cs              # get_issue, search_issues, list_issues
│   │   ├── TraceTools.cs              # search_traces, get_trace
│   │   ├── EventTools.cs              # search_events, get_event
│   │   ├── TelemetryTools.cs          # Existing qyl-native tools (unchanged)
│   │   ├── GenAiTools.cs              # Existing GenAI tools (unchanged)
│   │   ├── ReplayTools.cs             # Existing replay tools (unchanged)
│   │   ├── ConsoleTools.cs            # Existing console tools (unchanged)
│   │   ├── StructuredLogTools.cs      # Existing log tools (unchanged)
│   │   ├── BuildTools.cs              # Existing build tools (unchanged)
│   │   ├── StorageTools.cs            # Existing storage tools (unchanged)
│   │   ├── CopilotTools.cs            # Existing copilot tools (unchanged)
│   │   ├── ClaudeCodeTools.cs         # Existing claude code tools (unchanged)
│   │   ├── AnalyticsTools.cs          # Existing analytics tools (unchanged)
│   │   └── ServiceTools.cs            # Existing service tools (unchanged)
│   ├── Models/                        # Shared DTOs (Issue, Trace, User, Project)
│   └── Skills/                        # Skill groupings and filtering
│
├── qyl.mcp.sentry/                   # NEW — Sentry API client + backend
│   ├── SentryApiClient.cs             # HTTP client for Sentry REST API
│   ├── SentryBackend.cs               # IObservabilityBackend implementation
│   └── Models/                        # Sentry DTOs + mapping to shared models
│
├── qyl.mcp.cloud/                    # NEW — Azure Container App
│   ├── Program.cs                     # ASP.NET Core + Streamable HTTP transport
│   ├── OAuth/
│   │   ├── QylOAuthHandler.cs         # OAuth provider (RFC 9728)
│   │   ├── IdentityProviders/         # GitHub, Microsoft, Google
│   │   └── TokenVault.cs              # Per-user backend token storage
│   ├── Middleware/
│   │   ├── ConstraintMiddleware.cs    # /mcp/{org}/{project} path constraints
│   │   └── SkillFilterMiddleware.cs   # Skill-based tool filtering
│   ├── Endpoints/
│   │   ├── McpEndpoint.cs             # Streamable HTTP MCP endpoint
│   │   ├── WellKnown.cs               # RFC 9728 discovery
│   │   └── Metadata.cs                # /mcp.json, /llms.txt
│   └── Dockerfile
```

## Backend Interface

Provider-agnostic interface with capability flags.

```csharp
public interface IObservabilityBackend
{
    string Name { get; }                    // "qyl", "sentry", "seq"
    string DisplayName { get; }             // "qyl Native", "Sentry"
    BackendCapabilities Capabilities { get; }

    // Identity
    Task<BackendUser> GetAuthenticatedUser(CancellationToken ct = default);

    // Navigation
    Task<Project[]> ListProjects(CancellationToken ct = default);

    // Issues (read)
    Task<Issue[]> SearchIssues(IssueQuery query, CancellationToken ct = default);
    Task<Issue?> GetIssue(string issueId, CancellationToken ct = default);

    // Issues (write)
    Task<Issue> UpdateIssue(string issueId, IssueUpdate update, CancellationToken ct = default);

    // Traces
    Task<Trace[]> SearchTraces(TraceQuery query, CancellationToken ct = default);
    Task<Trace?> GetTrace(string traceId, CancellationToken ct = default);

    // Events/Logs
    Task<Event[]> SearchEvents(EventQuery query, CancellationToken ct = default);
}

[Flags]
public enum BackendCapabilities
{
    None          = 0,
    Identity      = 1 << 0,
    Issues        = 1 << 1,
    IssueWrite    = 1 << 2,
    Traces        = 1 << 3,
    Events        = 1 << 4,
    Projects      = 1 << 5,
    AiAnalysis    = 1 << 6,
}
```

Tools query the `BackendRegistry` which resolves by name and filters by capability:

```csharp
[McpServerToolType]
internal sealed class IssueTools(BackendRegistry registry)
{
    [McpServerTool(Name = "qyl.search_issues")]
    [Description("Search for issues across connected observability backends.")]
    public async Task<ToolResult<Issue[]>> SearchIssues(
        [Description("Backend name ('sentry', 'qyl'). Null = all backends.")]
        string? backend = null,
        [Description("Search query string")]
        string? query = null,
        [Description("Only include issues since this ISO timestamp")]
        DateTime? since = null)
    {
        var backends = registry.Resolve(backend, BackendCapabilities.Issues);
        var results = await backends.SelectManyAsync(
            b => b.SearchIssues(new IssueQuery(query, since)));
        return ToolResult.Ok(results);
    }
}
```

Existing qyl-native tools remain unchanged. They are qyl-specific and do not go through the backend interface.

## v0.1 Backends

1. **qyl-native** — wraps existing `qyl.collector` HTTP calls. Capabilities: Identity, Traces, Events.
2. **Sentry** — new `SentryApiClient` calling Sentry REST API. Capabilities: Identity, Issues, IssueWrite, Traces, Events, Projects, AiAnalysis.

## OAuth & Cloud Architecture

```
                    ┌──────────────────────────────────┐
                    │        MCP Client                │
                    │  (Claude, Cursor, VS Code)       │
                    └───────────────┬──────────────────┘
                                    │ HTTP + Bearer token
                                    ▼
                    ┌──────────────────────────────────┐
                    │     qyl.mcp.cloud                │
                    │     Azure Container App          │
                    │                                  │
                    │  /mcp                             │
                    │  /mcp/{org}                       │
                    │  /mcp/{org}/{project}             │
                    │                                  │
                    │  /oauth/authorize                 │
                    │  /oauth/callback                  │
                    │                                  │
                    │  /.well-known/oauth-protected-... │
                    │  /mcp.json, /llms.txt             │
                    └──────────┬───────────┬───────────┘
                               │           │
              ┌────────────────┘           └────────────────┐
              ▼                                             ▼
    ┌──────────────────┐                        ┌──────────────────┐
    │  qyl.collector   │                        │  Sentry API      │
    │  (DuckDB)        │                        │  (sentry.io)     │
    └──────────────────┘                        └──────────────────┘
```

### OAuth Flow

1. MCP Client connects to `https://mcp.qyl.dev/mcp`
2. Server returns `401` with `WWW-Authenticate` → `/.well-known/oauth-protected-resource`
3. Client starts OAuth flow → `GET /oauth/authorize`
4. qyl shows approval dialog (skill selection: observe, triage, navigate)
5. Redirects to Identity Provider (GitHub/Microsoft)
6. Callback stores identity + selected skills + backend tokens
7. MCP Client receives Bearer token

### Token Vault

Per-user storage in Azure Key Vault or Cosmos DB:
- qyl session token (always present)
- Sentry API token (from Sentry OAuth, optional)
- Future: Seq API key, Datadog API key

## Skill System

Skills group tools and are selected during OAuth.

| Skill       | Tools                                                          | Description              |
|-------------|----------------------------------------------------------------|--------------------------|
| **observe** | search_agent_runs, get_agent_run, get_token_usage, search_traces, get_trace | Read telemetry           |
| **inspect** | get_issue, search_issues, list_issues, search_events           | Investigate errors       |
| **triage**  | update_issue, resolve_issue                                    | Modify issues (write)    |
| **navigate**| whoami, list_projects, list_services                           | Orientation              |
| **analyze** | get_genai_stats, get_latency_stats, get_coverage_gaps          | AI-powered analysis      |

## Platform Connectors

### GitHub App / Marketplace

GitHub App that auto-configures MCP in repos on installation.
- Creates `.github/mcp.json` with qyl endpoint
- GitHub App OAuth flow stores GitHub token + creates qyl session

### Azure Copilot Extension

GitHub Copilot Extension (Azure Marketplace) for `@qyl` in Copilot Chat.
- Server-side agent calling qyl.mcp.cloud
- Azure AD / Microsoft identity

### VS Code Extension

Thin wrapper that registers MCP server in VS Code settings.
- Installs → configures `mcp.json` → opens OAuth in browser
- Published on VS Code Marketplace

### One-Click Setup

```bash
# Claude Code
claude mcp add --transport http qyl https://mcp.qyl.dev/mcp

# Cursor
{ "mcpServers": { "qyl": { "url": "https://mcp.qyl.dev/mcp" } } }
```

Landing page at `https://mcp.qyl.dev` with:
- One-click install buttons per client
- `/mcp.json` for auto-discovery
- `/llms.txt` for LLM-readable docs
- `/.well-known/oauth-protected-resource` for RFC 9728

## v0.1 Scope (Must-Have Tools)

From the user's scope recommendation:

**Must-have (read-only, high value):**
- `qyl.get_issue` — Get issue details
- `qyl.search_issues` — Search issues
- `qyl.list_issues` — List recent issues
- `qyl.search_events` — Search events
- `qyl.get_trace` — Get trace details
- `qyl.whoami` — Identity
- `qyl.list_projects` — Navigation

**Nice-to-have (write):**
- `qyl.update_issue` — Update issue status/assignment
- `qyl.resolve_issue` — Resolve an issue

**qyl-specific (no Sentry equivalent, already exist):**
- `qyl.search_spans` — DuckDB direct
- `qyl.get_telemetry_summary` — Aggregated stats
- `qyl.analyze_latency` — Latency analysis

## Decision: Interface First

Define `IObservabilityBackend` interface first, then implement both backends (qyl-native + Sentry) against it. This ensures tool signatures are stable before building platform connectors.
