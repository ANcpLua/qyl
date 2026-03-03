# qyl.mcp Platform

> Merged from: `plans/2026-03-01-qyl-mcp-platform-design.md` + `plans/2026-03-01-qyl-mcp-platform-plan.md`

## Implementation Status

| Feature | Status | Notes |
|---------|--------|-------|
| Tool annotations (ReadOnly, Destructive, Idempotent) | **DONE** | All tools annotated via `[McpServerTool]` attributes |
| Skills system (tool grouping + filtering) | **DONE** | `QylSkillKind` enum (8 categories), `QYL_SKILLS` env var, conditional DI registration |
| Error taxonomy | **DONE** | `CollectorHelper.ExecuteAsync` categorizes by HTTP status |
| Constraint scoping | **DONE** | `QylScope` + `ScopingDelegatingHandler` via `QYL_SERVICE`/`QYL_SESSION` env vars |
| Monolith split (core/cloud/sentry) | NOT STARTED | |
| Streamable HTTP transport | NOT STARTED | |
| OAuth (RFC 9728) | NOT STARTED | |
| IObservabilityBackend interface | NOT STARTED | |
| Sentry backend | NOT STARTED | |
| Platform connectors (GitHub App, VS Code) | NOT STARTED | |

> **Note:** The skills system was implemented differently from the original 5-tier design below.
> Actual implementation uses 8 skill categories (Inspect, Health, Analytics, Agent, Build, Anomaly, Copilot, ClaudeCode)
> with conditional DI registration in `Program.cs` instead of MCP filter-based approach.

---

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


---

# qyl.mcp Platform — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Split qyl.mcp into core + cloud + sentry packages with a provider-agnostic backend interface, Streamable HTTP transport, and OAuth.

**Architecture:** Monolith split — extract shared tools into `qyl.mcp.core`, add `qyl.mcp.sentry` for Sentry API, add `qyl.mcp.cloud` for Azure Container App with HTTP transport + OAuth. See Part 1 (Design) in this document.

**Tech Stack:** .NET 10, ASP.NET Core, ModelContextProtocol NuGet (v1.0.0), ANcpLua.NET.Sdk, source-generated JSON, CPM via Directory.Packages.props + Version.props.

**Design Doc:** See Part 1 (Design) above in this document.

---

### Task 1: Create qyl.mcp.core project and move shared code

**Files:**
- Create: `src/qyl.mcp.core/qyl.mcp.core.csproj`
- Move: `src/qyl.mcp/Tools/*.cs` → `src/qyl.mcp.core/Tools/`
- Move: `src/qyl.mcp/Auth/` → `src/qyl.mcp.core/Auth/`
- Move: `src/qyl.mcp/Client.cs` → `src/qyl.mcp.core/TelemetryConstants.cs`
- Move: `src/qyl.mcp/McpCollectorHttpClientExtensions.cs` → `src/qyl.mcp.core/`
- Modify: `src/qyl.mcp/qyl.mcp.csproj` — add ProjectReference to qyl.mcp.core
- Modify: `src/qyl.mcp/Program.cs` — thin down to just stdio transport registration

**Step 1: Create qyl.mcp.core.csproj**

```xml
<Project Sdk="ANcpLua.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="ANcpLua.Roslyn.Utilities"/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="ANcpLua.Roslyn.Utilities"/>
    <PackageReference Include="Microsoft.Extensions.Hosting"/>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions"/>
    <PackageReference Include="ModelContextProtocol"/>
    <PackageReference Include="OpenTelemetry.Api"/>
    <PackageReference Include="Microsoft.Extensions.Http"/>
    <PackageReference Include="Microsoft.Extensions.Http.Diagnostics"/>
    <PackageReference Include="Microsoft.Extensions.Http.Resilience"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\qyl.protocol\qyl.protocol.csproj"/>
  </ItemGroup>
</Project>
```

**Step 2: Move files**

```bash
cd /Users/ancplua/qyl
mkdir -p src/qyl.mcp.core/Tools src/qyl.mcp.core/Auth

# Move tools
mv src/qyl.mcp/Tools/*.cs src/qyl.mcp.core/Tools/

# Move auth
mv src/qyl.mcp/Auth/*.cs src/qyl.mcp.core/Auth/

# Move shared infra
mv src/qyl.mcp/Client.cs src/qyl.mcp.core/TelemetryConstants.cs
mv src/qyl.mcp/McpCollectorHttpClientExtensions.cs src/qyl.mcp.core/
```

**Step 3: Update namespaces**

Change all moved files from `namespace qyl.mcp` / `namespace qyl.mcp.Tools` / `namespace qyl.mcp.Auth` to `namespace qyl.mcp.core` / `namespace qyl.mcp.core.Tools` / `namespace qyl.mcp.core.Auth`.

**Step 4: Update qyl.mcp.csproj**

Remove the PackageReferences that moved to core. Add ProjectReference:

```xml
<ItemGroup>
  <ProjectReference Include="..\qyl.mcp.core\qyl.mcp.core.csproj"/>
</ItemGroup>
```

Keep only: `Microsoft.Extensions.Hosting`, `ModelContextProtocol` (for stdio transport registration).

**Step 5: Slim down qyl.mcp/Program.cs**

Program.cs becomes a thin host that references core and registers stdio transport. All tool types are resolved from the core assembly. The using directives change to `qyl.mcp.core.*`.

**Step 6: Build and verify**

```bash
cd /Users/ancplua/qyl
dotnet build src/qyl.mcp.core/qyl.mcp.core.csproj
dotnet build src/qyl.mcp/qyl.mcp.csproj
```

Expected: Both build with zero errors.

**Step 7: Commit**

```bash
git add src/qyl.mcp.core/ src/qyl.mcp/
git commit -m "refactor: extract qyl.mcp.core from qyl.mcp

Move tools, auth, and shared infrastructure into qyl.mcp.core.
qyl.mcp becomes a thin stdio host referencing core."
```

---

### Task 2: Define IObservabilityBackend interface and shared models

**Files:**
- Create: `src/qyl.mcp.core/Backends/IObservabilityBackend.cs`
- Create: `src/qyl.mcp.core/Backends/BackendCapabilities.cs`
- Create: `src/qyl.mcp.core/Backends/BackendRegistry.cs`
- Create: `src/qyl.mcp.core/Models/BackendUser.cs`
- Create: `src/qyl.mcp.core/Models/Issue.cs`
- Create: `src/qyl.mcp.core/Models/IssueQuery.cs`
- Create: `src/qyl.mcp.core/Models/IssueUpdate.cs`
- Create: `src/qyl.mcp.core/Models/Trace.cs`
- Create: `src/qyl.mcp.core/Models/TraceQuery.cs`
- Create: `src/qyl.mcp.core/Models/Event.cs`
- Create: `src/qyl.mcp.core/Models/EventQuery.cs`
- Create: `src/qyl.mcp.core/Models/Project.cs`

**Step 1: Create BackendCapabilities.cs**

```csharp
namespace qyl.mcp.core.Backends;

[Flags]
public enum BackendCapabilities
{
    None       = 0,
    Identity   = 1 << 0,
    Issues     = 1 << 1,
    IssueWrite = 1 << 2,
    Traces     = 1 << 3,
    Events     = 1 << 4,
    Projects   = 1 << 5,
    AiAnalysis = 1 << 6,
}
```

**Step 2: Create shared model records**

Each model file is a small record. Example `Issue.cs`:

```csharp
namespace qyl.mcp.core.Models;

public sealed record Issue(
    string Id,
    string ShortId,
    string Title,
    string Status,       // "unresolved", "resolved", "ignored"
    string? AssignedTo,
    string? Backend,     // "sentry", "qyl"
    string? ProjectSlug,
    DateTime FirstSeen,
    DateTime LastSeen,
    long EventCount,
    string? Url);
```

Same pattern for `BackendUser`, `Project`, `Trace`, `Event`. Query records like `IssueQuery(string? Query, DateTime? Since, int Limit = 25)`.

**Step 3: Create IObservabilityBackend.cs**

```csharp
namespace qyl.mcp.core.Backends;

public interface IObservabilityBackend
{
    string Name { get; }
    string DisplayName { get; }
    BackendCapabilities Capabilities { get; }

    Task<BackendUser> GetAuthenticatedUser(CancellationToken ct = default);
    Task<Project[]> ListProjects(CancellationToken ct = default);
    Task<Issue[]> SearchIssues(IssueQuery query, CancellationToken ct = default);
    Task<Issue?> GetIssue(string issueId, CancellationToken ct = default);
    Task<Issue> UpdateIssue(string issueId, IssueUpdate update, CancellationToken ct = default);
    Task<Trace[]> SearchTraces(TraceQuery query, CancellationToken ct = default);
    Task<Trace?> GetTrace(string traceId, CancellationToken ct = default);
    Task<Event[]> SearchEvents(EventQuery query, CancellationToken ct = default);
}
```

**Step 4: Create BackendRegistry.cs**

```csharp
namespace qyl.mcp.core.Backends;

public sealed class BackendRegistry(IEnumerable<IObservabilityBackend> backends)
{
    private readonly IReadOnlyList<IObservabilityBackend> _backends = backends.ToList();

    public IReadOnlyList<IObservabilityBackend> Resolve(
        string? name = null,
        BackendCapabilities required = BackendCapabilities.None)
    {
        var query = _backends.AsEnumerable();

        if (!string.IsNullOrEmpty(name))
            query = query.Where(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (required != BackendCapabilities.None)
            query = query.Where(b => b.Capabilities.HasFlag(required));

        return query.ToList();
    }
}
```

**Step 5: Build**

```bash
dotnet build src/qyl.mcp.core/qyl.mcp.core.csproj
```

Expected: PASS.

**Step 6: Commit**

```bash
git add src/qyl.mcp.core/Backends/ src/qyl.mcp.core/Models/
git commit -m "feat(core): add IObservabilityBackend interface and shared models

Provider-agnostic backend interface with capability flags,
BackendRegistry for resolution, and shared DTOs (Issue, Trace,
Event, Project, BackendUser)."
```

---

### Task 3: Create cross-backend MCP tools (IssueTools, TraceTools, IdentityTools)

**Files:**
- Create: `src/qyl.mcp.core/Tools/IssueTools.cs`
- Create: `src/qyl.mcp.core/Tools/TraceTools.cs`
- Create: `src/qyl.mcp.core/Tools/IdentityTools.cs`
- Create: `src/qyl.mcp.core/Tools/NavigationTools.cs`
- Create: `src/qyl.mcp.core/Tools/EventTools.cs`

**Step 1: Create IssueTools.cs**

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;
using qyl.mcp.core.Backends;

namespace qyl.mcp.core.Tools;

[McpServerToolType]
internal sealed class IssueTools(BackendRegistry registry)
{
    [McpServerTool(Name = "qyl.search_issues")]
    [Description("""
        Search for issues across connected observability backends.
        Filter by query string, time range, and specific backend.
        Returns: List of issues with title, status, and event count.
        """)]
    public async Task<string> SearchIssuesAsync(
        [Description("Backend name ('sentry', 'qyl'). Null = all backends.")] string? backend = null,
        [Description("Search query string")] string? query = null,
        [Description("Only include issues since this ISO timestamp")] DateTime? since = null,
        [Description("Maximum results (default: 25)")] int limit = 25,
        CancellationToken ct = default)
    {
        var backends = registry.Resolve(backend, BackendCapabilities.Issues);
        if (backends.Count == 0) return "No backends with issue support are connected.";

        var tasks = backends.Select(b => b.SearchIssues(new IssueQuery(query, since, limit), ct));
        var results = (await Task.WhenAll(tasks).ConfigureAwait(false)).SelectMany(r => r).ToList();

        if (results.Count == 0) return "No issues found matching the criteria.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Issues ({results.Count} results)");
        sb.AppendLine();
        foreach (var issue in results.Take(limit))
        {
            sb.AppendLine($"- **[{issue.ShortId}]** {issue.Title}");
            sb.AppendLine($"  Status: {issue.Status} | Events: {issue.EventCount} | Backend: {issue.Backend}");
            if (issue.Url is not null) sb.AppendLine($"  URL: {issue.Url}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    [McpServerTool(Name = "qyl.get_issue")]
    [Description("""
        Get detailed information about a specific issue.
        The issue ID format depends on the backend (e.g., 'PROJECT-123' for Sentry).
        Returns: Full issue details including status, assignment, and event count.
        """)]
    public async Task<string> GetIssueAsync(
        [Description("Issue ID (e.g., 'PROJECT-123')")] string issueId,
        [Description("Backend name. Required if issue ID is ambiguous.")] string? backend = null,
        CancellationToken ct = default)
    {
        var backends = registry.Resolve(backend, BackendCapabilities.Issues);
        if (backends.Count == 0) return "No backends with issue support are connected.";

        foreach (var b in backends)
        {
            var issue = await b.GetIssue(issueId, ct).ConfigureAwait(false);
            if (issue is not null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# {issue.ShortId}: {issue.Title}");
                sb.AppendLine();
                sb.AppendLine($"- **Status:** {issue.Status}");
                sb.AppendLine($"- **Assigned To:** {issue.AssignedTo ?? "Unassigned"}");
                sb.AppendLine($"- **Events:** {issue.EventCount}");
                sb.AppendLine($"- **First Seen:** {issue.FirstSeen:u}");
                sb.AppendLine($"- **Last Seen:** {issue.LastSeen:u}");
                sb.AppendLine($"- **Backend:** {issue.Backend}");
                if (issue.Url is not null) sb.AppendLine($"- **URL:** {issue.Url}");
                return sb.ToString();
            }
        }
        return $"Issue '{issueId}' not found in any connected backend.";
    }

    [McpServerTool(Name = "qyl.update_issue")]
    [Description("""
        Update an issue's status or assignment.
        Requires a backend with write capabilities.
        Valid statuses: 'resolved', 'unresolved', 'ignored'.
        """)]
    public async Task<string> UpdateIssueAsync(
        [Description("Issue ID (e.g., 'PROJECT-123')")] string issueId,
        [Description("Backend name (required for write operations)")] string backend,
        [Description("New status: 'resolved', 'unresolved', 'ignored'")] string? status = null,
        [Description("Assign to user or team (e.g., 'user:123', 'team:my-team')")] string? assignedTo = null,
        CancellationToken ct = default)
    {
        var backends = registry.Resolve(backend, BackendCapabilities.IssueWrite);
        if (backends.Count == 0) return $"Backend '{backend}' does not support issue writes.";

        if (status is null && assignedTo is null)
            return "At least one of 'status' or 'assignedTo' must be provided.";

        var b = backends[0];
        var updated = await b.UpdateIssue(issueId, new IssueUpdate(status, assignedTo), ct).ConfigureAwait(false);
        return $"Issue {updated.ShortId} updated in {b.DisplayName}. Status: {updated.Status}, Assigned: {updated.AssignedTo ?? "Unassigned"}.";
    }
}
```

**Step 2: Create IdentityTools.cs, NavigationTools.cs, TraceTools.cs, EventTools.cs**

Same pattern — each has 1-3 methods that query `BackendRegistry`, fan out to backends, aggregate results, return markdown.

**Step 3: Register new tools in Program.cs**

Add `.WithTools<IssueTools>()`, `.WithTools<TraceTools>()` etc. to the MCP server builder in both `qyl.mcp/Program.cs` (stdio) and later `qyl.mcp.cloud/Program.cs` (HTTP).

**Step 4: Build**

```bash
dotnet build src/qyl.mcp.core/qyl.mcp.core.csproj
dotnet build src/qyl.mcp/qyl.mcp.csproj
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/qyl.mcp.core/Tools/ src/qyl.mcp/Program.cs
git commit -m "feat(core): add cross-backend MCP tools

IssueTools, TraceTools, IdentityTools, NavigationTools, EventTools —
all query BackendRegistry for provider-agnostic multi-backend support."
```

---

### Task 4: Create qyl.mcp.sentry — Sentry API client + backend

**Files:**
- Create: `src/qyl.mcp.sentry/qyl.mcp.sentry.csproj`
- Create: `src/qyl.mcp.sentry/SentryApiClient.cs`
- Create: `src/qyl.mcp.sentry/SentryBackend.cs`
- Create: `src/qyl.mcp.sentry/SentryOptions.cs`
- Create: `src/qyl.mcp.sentry/Models/SentryIssue.cs`
- Create: `src/qyl.mcp.sentry/Models/SentryUser.cs`
- Create: `src/qyl.mcp.sentry/Models/SentryProject.cs`
- Create: `src/qyl.mcp.sentry/Models/SentryEvent.cs`
- Create: `src/qyl.mcp.sentry/Models/SentryJsonContext.cs`
- Create: `src/qyl.mcp.sentry/SentryServiceExtensions.cs`

**Step 1: Create qyl.mcp.sentry.csproj**

```xml
<Project Sdk="ANcpLua.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="ANcpLua.Roslyn.Utilities"/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="ANcpLua.Roslyn.Utilities"/>
    <PackageReference Include="Microsoft.Extensions.Http"/>
    <PackageReference Include="Microsoft.Extensions.Http.Resilience"/>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\qyl.mcp.core\qyl.mcp.core.csproj"/>
  </ItemGroup>
</Project>
```

**Step 2: Create SentryOptions.cs**

```csharp
namespace qyl.mcp.sentry;

public sealed class SentryOptions
{
    public const string SectionName = "Sentry";
    public string BaseUrl { get; set; } = "https://sentry.io";
    public string? AuthToken { get; set; }
    public string? OrganizationSlug { get; set; }
}
```

**Step 3: Create SentryApiClient.cs**

HTTP client calling Sentry REST API v0. Key endpoints:
- `GET /api/0/` — authenticated user
- `GET /api/0/organizations/{org}/issues/` — search issues
- `GET /api/0/organizations/{org}/issues/{id}/` — get issue
- `PUT /api/0/organizations/{org}/issues/{id}/` — update issue
- `GET /api/0/organizations/{org}/projects/` — list projects
- `GET /api/0/organizations/{org}/events/` — search events
- `GET /api/0/organizations/{org}/events/{id}/` — get event

```csharp
namespace qyl.mcp.sentry;

public sealed partial class SentryApiClient(HttpClient client, ILogger<SentryApiClient> logger)
{
    public async Task<SentryUser> GetAuthenticatedUserAsync(CancellationToken ct = default)
    {
        var response = await client.GetFromJsonAsync<SentryAuthResponse>(
            "/api/0/", SentryJsonContext.Default.SentryAuthResponse, ct).ConfigureAwait(false);
        return response?.User ?? throw new InvalidOperationException("Failed to get authenticated user");
    }

    public async Task<SentryIssue[]> SearchIssuesAsync(
        string orgSlug, string? query, int limit, CancellationToken ct = default)
    {
        var url = $"/api/0/organizations/{Uri.EscapeDataString(orgSlug)}/issues/?limit={limit}";
        if (!string.IsNullOrEmpty(query))
            url += $"&query={Uri.EscapeDataString(query)}";

        return await client.GetFromJsonAsync<SentryIssue[]>(
            url, SentryJsonContext.Default.SentryIssueArray, ct).ConfigureAwait(false) ?? [];
    }

    // ... similar methods for getIssue, updateIssue, listProjects, searchEvents
}
```

**Step 4: Create SentryBackend.cs implementing IObservabilityBackend**

Maps Sentry DTOs to shared models. Sets `Capabilities = Identity | Issues | IssueWrite | Projects | Events | Traces`.

**Step 5: Create SentryServiceExtensions.cs**

```csharp
public static class SentryServiceExtensions
{
    public static IServiceCollection AddSentryBackend(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SentryOptions>(configuration.GetSection(SentryOptions.SectionName));
        services.AddHttpClient<SentryApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SentryOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            if (options.AuthToken is not null)
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.AuthToken);
        }).AddStandardResilienceHandler();

        services.AddSingleton<IObservabilityBackend, SentryBackend>();
        return services;
    }
}
```

**Step 6: Create source-generated SentryJsonContext**

```csharp
[JsonSerializable(typeof(SentryUser))]
[JsonSerializable(typeof(SentryIssue[]))]
[JsonSerializable(typeof(SentryProject[]))]
[JsonSerializable(typeof(SentryAuthResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class SentryJsonContext : JsonSerializerContext;
```

**Step 7: Build**

```bash
dotnet build src/qyl.mcp.sentry/qyl.mcp.sentry.csproj
```

Expected: PASS.

**Step 8: Commit**

```bash
git add src/qyl.mcp.sentry/
git commit -m "feat(sentry): add Sentry API client and IObservabilityBackend implementation

SentryApiClient calls Sentry REST API v0. SentryBackend maps to shared
models. AOT-ready with source-generated JSON contexts."
```

---

### Task 5: Create QylNativeBackend implementing IObservabilityBackend

**Files:**
- Create: `src/qyl.mcp.core/Backends/QylNativeBackend.cs`
- Modify: `src/qyl.mcp.core/Tools/HttpTelemetryStore.cs` — reuse for native backend

**Step 1: Create QylNativeBackend.cs**

Wraps the existing `HttpTelemetryStore` and `StorageTools` HTTP calls. Maps existing qyl.collector responses to shared models.

```csharp
namespace qyl.mcp.core.Backends;

public sealed class QylNativeBackend(HttpClient client) : IObservabilityBackend
{
    public string Name => "qyl";
    public string DisplayName => "qyl Native";
    public BackendCapabilities Capabilities =>
        BackendCapabilities.Identity | BackendCapabilities.Traces | BackendCapabilities.Events;

    public Task<BackendUser> GetAuthenticatedUser(CancellationToken ct) =>
        Task.FromResult(new BackendUser("qyl-local", "Local User", "local@qyl.dev", "qyl"));

    public Task<Project[]> ListProjects(CancellationToken ct) =>
        Task.FromResult(Array.Empty<Project>()); // qyl-native doesn't have projects

    public Task<Issue[]> SearchIssues(IssueQuery query, CancellationToken ct) =>
        Task.FromResult(Array.Empty<Issue>()); // qyl-native doesn't track issues

    public Task<Issue?> GetIssue(string issueId, CancellationToken ct) =>
        Task.FromResult<Issue?>(null);

    public Task<Issue> UpdateIssue(string issueId, IssueUpdate update, CancellationToken ct) =>
        throw new NotSupportedException("qyl-native does not support issue writes");

    public async Task<Trace[]> SearchTraces(TraceQuery query, CancellationToken ct)
    {
        // Call qyl.collector /api/v1/genai/spans endpoint
        // Map to shared Trace model
    }

    public async Task<Trace?> GetTrace(string traceId, CancellationToken ct)
    {
        // Call qyl.collector /api/v1/sessions/{traceId}/spans
        // Map to shared Trace model
    }

    public async Task<Event[]> SearchEvents(EventQuery query, CancellationToken ct)
    {
        // Call qyl.collector /api/v1/logs or /api/v1/events
        // Map to shared Event model
    }
}
```

**Step 2: Register in DI**

```csharp
services.AddSingleton<IObservabilityBackend>(sp =>
    new QylNativeBackend(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("qyl-native")));
services.AddSingleton<BackendRegistry>();
```

**Step 3: Build and test**

```bash
dotnet build src/qyl.mcp.core/qyl.mcp.core.csproj
dotnet build src/qyl.mcp/qyl.mcp.csproj
```

**Step 4: Commit**

```bash
git add src/qyl.mcp.core/Backends/ src/qyl.mcp/Program.cs
git commit -m "feat(core): add QylNativeBackend wrapping collector HTTP calls

Implements IObservabilityBackend with Traces + Events capabilities.
Identity returns local user. Issues not supported (returns empty)."
```

---

### Task 6: Create qyl.mcp.cloud — ASP.NET Core with Streamable HTTP transport

**Files:**
- Create: `src/qyl.mcp.cloud/qyl.mcp.cloud.csproj`
- Create: `src/qyl.mcp.cloud/Program.cs`
- Create: `src/qyl.mcp.cloud/Endpoints/McpEndpoints.cs`
- Create: `src/qyl.mcp.cloud/Endpoints/MetadataEndpoints.cs`
- Create: `src/qyl.mcp.cloud/Endpoints/WellKnownEndpoints.cs`
- Create: `src/qyl.mcp.cloud/Dockerfile`
- Create: `src/qyl.mcp.cloud/Properties/launchSettings.json`

**Step 1: Create qyl.mcp.cloud.csproj**

```xml
<Project Sdk="ANcpLua.NET.Sdk/Web">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="ANcpLua.Roslyn.Utilities"/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="ANcpLua.Roslyn.Utilities"/>
    <PackageReference Include="ModelContextProtocol"/>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\qyl.mcp.core\qyl.mcp.core.csproj"/>
    <ProjectReference Include="..\qyl.mcp.sentry\qyl.mcp.sentry.csproj"/>
  </ItemGroup>
</Project>
```

**Step 2: Create Program.cs**

```csharp
using qyl.mcp.core.Auth;
using qyl.mcp.core.Backends;
using qyl.mcp.core.Tools;
using qyl.mcp.sentry;

var builder = WebApplication.CreateBuilder(args);

// Auth & backends
builder.Services.AddMcpAuth(builder.Configuration);
builder.Services.AddSentryBackend(builder.Configuration);
builder.Services.AddSingleton<BackendRegistry>();

// Collector URL for qyl-native backend
var collectorUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
// ... register QylNativeBackend + collector tool clients

// MCP server with Streamable HTTP transport
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<IssueTools>()
    .WithTools<TraceTools>()
    .WithTools<IdentityTools>()
    .WithTools<NavigationTools>()
    .WithTools<EventTools>()
    // Keep existing qyl-native tools
    .WithTools<TelemetryTools>()
    .WithTools<StorageTools>()
    // ... etc
    ;

var app = builder.Build();

// MCP Streamable HTTP endpoint
app.MapMcp("/mcp");
app.MapMcp("/mcp/{org}");
app.MapMcp("/mcp/{org}/{project}");

// Metadata endpoints
app.MapGet("/mcp.json", () => Results.Json(new { name = "qyl", endpoint = "/mcp" }));
app.MapGet("/llms.txt", () => Results.Text("# qyl MCP Server\n..."));
app.MapGet("/.well-known/oauth-protected-resource", (HttpContext ctx) =>
    Results.Json(new { resource = $"{ctx.Request.Scheme}://{ctx.Request.Host}/mcp" }));

app.Run();
```

**Step 3: Create Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/qyl.mcp.cloud/qyl.mcp.cloud.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "qyl.mcp.cloud.dll"]
```

**Step 4: Build and run locally**

```bash
dotnet build src/qyl.mcp.cloud/qyl.mcp.cloud.csproj
dotnet run --project src/qyl.mcp.cloud -- --urls http://localhost:5200
```

Test with: `curl http://localhost:5200/mcp.json`

Expected: `{"name":"qyl","endpoint":"/mcp"}`

**Step 5: Commit**

```bash
git add src/qyl.mcp.cloud/
git commit -m "feat(cloud): add qyl.mcp.cloud with Streamable HTTP transport

ASP.NET Core app serving MCP over HTTP at /mcp with path constraints.
Includes mcp.json, llms.txt, and RFC 9728 discovery endpoints.
References both qyl.mcp.core and qyl.mcp.sentry."
```

---

### Task 7: Add Skill system — tool filtering by skill groups

**Files:**
- Create: `src/qyl.mcp.core/Skills/Skill.cs`
- Create: `src/qyl.mcp.core/Skills/SkillRegistry.cs`
- Create: `src/qyl.mcp.core/Skills/ToolSkillAttribute.cs`

**Step 1: Define skills**

```csharp
namespace qyl.mcp.core.Skills;

public enum Skill { Observe, Inspect, Triage, Navigate, Analyze }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ToolSkillAttribute(Skill skill) : Attribute
{
    public Skill Skill { get; } = skill;
}
```

**Step 2: Annotate tools with skills**

```csharp
[McpServerTool(Name = "qyl.search_issues")]
[ToolSkill(Skill.Inspect)]
public async Task<string> SearchIssuesAsync(...) { ... }

[McpServerTool(Name = "qyl.update_issue")]
[ToolSkill(Skill.Triage)]
public async Task<string> UpdateIssueAsync(...) { ... }
```

**Step 3: Create SkillRegistry**

Resolves which tools are visible for a given set of granted skills. Used by the cloud project's skill filter middleware.

**Step 4: Build and commit**

```bash
dotnet build src/qyl.mcp.core/qyl.mcp.core.csproj
git add src/qyl.mcp.core/Skills/
git commit -m "feat(core): add skill system for tool grouping and filtering

Skills: Observe, Inspect, Triage, Navigate, Analyze.
ToolSkillAttribute annotates tools. SkillRegistry resolves visibility."
```

---

### Task 8: Add OAuth to qyl.mcp.cloud

**Files:**
- Create: `src/qyl.mcp.cloud/OAuth/QylOAuthHandler.cs`
- Create: `src/qyl.mcp.cloud/OAuth/TokenStore.cs`
- Create: `src/qyl.mcp.cloud/OAuth/OAuthEndpoints.cs`
- Modify: `src/qyl.mcp.cloud/Program.cs` — register OAuth middleware

**Step 1: Implement OAuth 2.0 authorization code flow**

- `GET /oauth/authorize` — show approval dialog with skill selection
- `POST /oauth/authorize` — redirect to GitHub/Microsoft identity
- `GET /oauth/callback` — exchange code for token, store in TokenStore
- Issue MCP bearer token for the MCP client

**Step 2: Implement TokenStore**

In-memory for dev, Azure Cosmos DB or Table Storage for production. Stores per-user:
- qyl session token
- Sentry auth token (optional)
- Granted skills

**Step 3: Wire up in Program.cs**

```csharp
app.MapGet("/oauth/authorize", OAuthEndpoints.Authorize);
app.MapPost("/oauth/authorize", OAuthEndpoints.HandleApproval);
app.MapGet("/oauth/callback", OAuthEndpoints.Callback);
```

**Step 4: Build and test locally**

```bash
dotnet build src/qyl.mcp.cloud/qyl.mcp.cloud.csproj
# Test: curl http://localhost:5200/.well-known/oauth-protected-resource
```

**Step 5: Commit**

```bash
git add src/qyl.mcp.cloud/OAuth/
git commit -m "feat(cloud): add OAuth 2.0 authorization flow

Approval dialog with skill selection, GitHub/Microsoft identity redirect,
token exchange and per-user token storage. RFC 9728 compliant."
```

---

### Task 9: Integration test — full stdio round-trip

**Files:**
- Create: `tests/qyl.mcp.tests/StdioIntegrationTests.cs`

**Step 1: Write test that starts qyl.mcp as a process, sends MCP initialize + tools/list, verifies cross-backend tools appear alongside existing tools.**

**Step 2: Run**

```bash
dotnet test tests/qyl.mcp.tests/
```

**Step 3: Commit**

```bash
git add tests/
git commit -m "test: add stdio integration test for cross-backend tools"
```

---

### Task 10: Integration test — HTTP transport round-trip

**Files:**
- Create: `tests/qyl.mcp.cloud.tests/HttpIntegrationTests.cs`

**Step 1: Use WebApplicationFactory to spin up qyl.mcp.cloud, send HTTP MCP requests, verify tools/list and tool invocation.**

**Step 2: Run and commit**

```bash
dotnet test tests/qyl.mcp.cloud.tests/
git add tests/
git commit -m "test: add HTTP transport integration test for cloud project"
```

---

### Summary: Implementation Order

| Task | What | Dependencies |
|------|------|-------------|
| 1 | Extract qyl.mcp.core | None |
| 2 | IObservabilityBackend + models | Task 1 |
| 3 | Cross-backend tools | Task 2 |
| 4 | qyl.mcp.sentry | Task 2 |
| 5 | QylNativeBackend | Task 2 |
| 6 | qyl.mcp.cloud (HTTP transport) | Tasks 3, 4, 5 |
| 7 | Skill system | Task 3 |
| 8 | OAuth | Task 6 |
| 9 | Stdio integration test | Tasks 3, 5 |
| 10 | HTTP integration test | Task 6 |

Tasks 3, 4, 5 can run in parallel after Task 2.
Tasks 9, 10 can run in parallel after their dependencies.
