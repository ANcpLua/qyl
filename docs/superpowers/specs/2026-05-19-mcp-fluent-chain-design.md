# Spec: `ANcpLua.Agents.Mcp` fluent chain — 2026-05-19

## Goal

Extract every generic MCP-server building block out of `services/qyl.mcp/` into the
`ANcpLua.Agents.Mcp` + `ANcpLua.Agents.Mcp.Hosting` package pair. Surface the result as
a fluent chain on `IMcpServerBuilder` so a consumer's host file shrinks to book-shape —
one `Program.cs` reading top-to-bottom, no `services.AddQylX()` siblings to
`AddMcpServer()`. qyl keeps only domain code (collector wiring, tools, apps, prompts,
scope definition, capability definitions, server metadata).

## End-state consumer surface

Two book-style hosts. Both compile against `ModelContextProtocol` 1.3.0 and the two
new packages.

### Stdio

```csharp
using qyl.mcp.Scoping;
using qyl.mcp.Tools;
using ANcpLua.Agents.Mcp.Hosting;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddQylMcpStdioConsole();

builder.Services.AddSingleton(QylScope.FromEnvironment());
builder.Services.AddSingleton<IQylConstraintInjector<QylScope>, QylScopeInjector>();

builder.Services
    .AddMcpServer()
    .WithToolsFromAssembly(typeof(QylTools).Assembly)
    .WithStdioServerTransport()
    .WithAnthropicResultSizeMeta(10_000)
    .WithQylScopeInjection<QylScope>()
    .WithQylAdminFilter(o =>
    {
        o.RequiredRole = "qyl:admin";
        o.AdminToolNames = QylTools.AdminTools;
    });

await builder.Build().RunAsync();
```

### HTTP

```csharp
using qyl.mcp.Scoping;
using qyl.mcp.Tools;
using ANcpLua.Agents.Mcp.Hosting;
using ANcpLua.Agents.Mcp.Hosting.Authentication;
using Microsoft.AspNetCore.Builder;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseQylMcpPortFallback(builder.Configuration);

builder.Services.AddSingleton(QylScope.FromEnvironment());
builder.Services.AddSingleton<IQylConstraintInjector<QylScope>, QylScopeInjector>();

builder.Services
    .AddMcpServer()
    .WithToolsFromAssembly(typeof(QylTools).Assembly)
    .WithHttpTransport(o => o.Stateless = true)
    .WithQylOAuthProtectedResource(o =>
    {
        o.Authority = "https://idp.example.com/realms/qyl";
        o.Audience = "qyl-mcp";
        o.ResolveResourceUrl = req => new Uri($"{req.Scheme}://{req.Host}/mcp");
    })
    .WithAnthropicResultSizeMeta(10_000)
    .WithQylScopeInjection<QylScope>()
    .WithQylAdminFilter(o =>
    {
        o.RequiredRole = "qyl:admin";
        o.AdminToolNames = QylTools.AdminTools;
    });

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapQylMcp().RequireAuthorization();
await app.RunAsync();
```

## Library surface — signatures + file paths

All new types live under one of the two existing packages. No new packages.

### `ANcpLua.Agents.Mcp` (no AspNetCore)

| File | Content |
|------|---------|
| `Scoping/IQylConstraintInjector.cs` | `public interface IQylConstraintInjector<in TScope> where TScope : class { IDictionary<string, JsonElement>? Inject(IDictionary<string, JsonElement>? arguments, TScope scope); }` |

### `ANcpLua.Agents.Mcp.Hosting` (AspNetCore-dependent)

| File | Surface |
|------|---------|
| `Logging/QylMcpLoggingExtensions.cs` | `public static ILoggingBuilder AddQylMcpStdioConsole(this ILoggingBuilder logging);` — calls `AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)`. Verbatim replacement for qyl's current `ConfigureLogging(logging, stdioTransport: true)` body. |
| `Hosting/QylMcpWebHostExtensions.cs` | `public static IWebHostBuilder UseQylMcpPortFallback(this IWebHostBuilder webHost, IConfiguration configuration);` — verbatim move of qyl's `ApplyPortFallback`. |
| `Authentication/QylOAuthOptions.cs` | `public sealed class QylOAuthOptions { public required string Authority { get; set; } public required string Audience { get; set; } public required Func<HttpRequest, Uri> ResolveResourceUrl { get; set; } public Action<QylProtectedResourceMetadataOptions>? ConfigureMetadata { get; set; } public Action<JwtBearerEvents>? ConfigureJwtEvents { get; set; } }` |
| `Authentication/QylProtectedResourceMetadataOptions.cs` | `public sealed class QylProtectedResourceMetadataOptions { public string[] BearerMethodsSupported { get; set; } = ["header"]; public string? ResourceName { get; set; } public Uri? ResourceDocumentation { get; set; } }` |
| `Authentication/QylMcpOAuthExtensions.cs` | `public static IMcpServerBuilder WithQylOAuthProtectedResource(this IMcpServerBuilder builder, Action<QylOAuthOptions> configure);` — wires `services.AddAuthentication(McpAuthenticationDefaults.AuthenticationScheme).AddJwtBearer(...).AddMcp(o => o.Events.OnResourceMetadataRequest = ...);` internally. |
| `Filters/QylMcpResultSizeFilter.cs` | `public static IMcpServerBuilder WithAnthropicResultSizeMeta(this IMcpServerBuilder builder, int thresholdChars = 10_000);` — registers a `CallToolFilter` that sums text-block chars on the response and sets `result.Meta["anthropic/maxResultSizeChars"]` when over threshold. |
| `Filters/QylMcpScopeInjectionFilter.cs` | `public static IMcpServerBuilder WithQylScopeInjection<TScope>(this IMcpServerBuilder builder) where TScope : class;` — resolves `TScope` + `IQylConstraintInjector<TScope>` from request DI scope, rewrites `request.Params.Arguments`. |
| `Filters/QylAdminFilterOptions.cs` | `public sealed class QylAdminFilterOptions { public required string RequiredRole { get; set; } public required IReadOnlySet<string> AdminToolNames { get; set; } public Func<HttpContext, IReadOnlySet<string>>? ResolveRoles { get; set; } }` |
| `Filters/QylMcpAdminFilter.cs` | `public static IMcpServerBuilder WithQylAdminFilter(this IMcpServerBuilder builder, Action<QylAdminFilterOptions> configure);` — registers a `CallToolFilter` that early-returns a denial `CallToolResult` when the tool is in `AdminToolNames` and the resolved-role set lacks `RequiredRole`. `ResolveRoles` defaults to reading `ClaimsPrincipal.FindAll(ClaimTypes.Role)`. |
| `Facades/QylMcpServerExtensions.cs` (extend existing) | Add overload `MapQylMcp(this IEndpointRouteBuilder endpoints, string pattern = "/mcp", bool mapHealthEndpoints = true)`. When `mapHealthEndpoints` is true, also `MapHealthChecks("/alive", { Predicate = c => c.Tags.Contains("live") })` and `MapHealthChecks("/health", { Predicate = c => c.Tags.Contains("ready") })`. |

Total: 1 new file in `.Mcp`, 9 new files + 1 modified file in `.Mcp.Hosting`.

### csproj edits (lead does these in Phase 0)

`src/ANcpLua.Agents.Mcp.Hosting/ANcpLua.Agents.Mcp.Hosting.csproj` — add:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer"/>
<PackageReference Include="Microsoft.Extensions.Logging.Console"/>
```

No version literals — central package management is in effect in both repos.

## Rules of engagement (from user)

1. MAF + ModelContextProtocol abstractions only. No parallel types. `IMcpServerBuilder` is the chain anchor. Options bags (`QylOAuthOptions`, `QylAdminFilterOptions`) live in the library.
2. SDK ≥ 1.3.0 pinned (confirmed: `Version.props:85-87`). Anything in qyl that duplicates 1.3.0 SDK behavior gets deleted in the same commit.
3. qyl public API breaks are free. No `[Obsolete]` ladder, no compat shims, no dead-type retention.
4. End-state IS the fluent chain. No `services.AddQylX()` siblings to `.AddMcpServer()`. `builder.Logging.AddQylMcpStdioConsole()` and `builder.WebHost.UseQylMcpPortFallback()` are the only `.AddX()` calls allowed outside the chain (different builders).
5. Tests follow the same shape. Each test arranges a real `WebApplicationBuilder` / `HostApplicationBuilder`, runs the chain, exercises the resulting `WebApplication` / `IHost`. Behavioral assertions only — `.Should().Contain(...)` / response inspection, not `options.X.Should().Be(...)`.

## qyl-side rewire deltas

Net change in `services/qyl.mcp/`: roughly **−350 LOC**, **+1 file** (`QylScopeInjector.cs`).

| qyl file | Change |
|----------|--------|
| `Hosting/QylMcpServiceCollectionExtensions.cs` lines 37-44 (`ConfigureLogging`) | Delete. Replace call sites with `builder.Logging.AddQylMcpStdioConsole()`. |
| `Hosting/QylMcpServiceCollectionExtensions.cs` lines 105-194 (`AddQylMcpHttpAuthentication`) | Delete the inline `AddAuthentication/AddJwtBearer/AddMcp` block. Replace with chain call `.WithQylOAuthProtectedResource(o => { ... })` in `QylMcpServerRegistration`. The 4 `LoggerMessage` partial methods (LogDiscoveryHit etc.) survive as qyl-side logging callbacks passed through `ConfigureJwtEvents`. |
| `Hosting/QylMcpServiceCollectionExtensions.cs` lines 213-226 (`ApplyPortFallback`) | Delete. Replace call site with `builder.WebHost.UseQylMcpPortFallback(builder.Configuration)`. |
| `Hosting/QylMcpServerRegistration.cs` lines 67-102 (the `WithRequestFilters` lambda) | Replace the three inline filter blocks with chain calls: `.WithQylAdminFilter(o => ...).WithQylScopeInjection<QylScope>().WithAnthropicResultSizeMeta(10_000)`. |
| `Hosting/QylMcpHttpHost.cs` lines 52-55 | Delete the inline `MapHealthChecks` calls. `MapQylMcp(mapHealthEndpoints: true)` covers it. |
| `Auth/McpAdminToolFilter.cs` | Delete file. The admin filter is registered via `.WithQylAdminFilter(o => { o.RequiredRole = "qyl:admin"; o.AdminToolNames = QylTools.AdminTools; o.ResolveRoles = ctx => keycloak.GetCachedRoles(); });` |
| `Scoping/ConstraintInjector.cs` | Replace static class with `public sealed class QylScopeInjector : IQylConstraintInjector<QylScope>` implementing the existing `InjectScope` logic. Wired in DI via `AddSingleton<IQylConstraintInjector<QylScope>, QylScopeInjector>()`. |
| `qyl.mcp.csproj` | Add `<PackageReference Include="ANcpLua.Agents.Mcp" />` and `<PackageReference Include="ANcpLua.Agents.Mcp.Hosting" />`. Remove dead usings. |
| qyl `Directory.Packages.props` | Add `<PackageVersion Include="ANcpLua.Agents.Mcp" Version="$(ANcpLuaAgentsVersion)"/>` and `<PackageVersion Include="ANcpLua.Agents.Mcp.Hosting" Version="$(ANcpLuaAgentsVersion)"/>`. |

## Out of scope (stays in qyl)

- `Scoping/ScopingDelegatingHandler.cs` — sets `gen_ai.conversation.id` Activity tag = qyl OTel boundary.
- `Scoping/QylScope.cs` — qyl domain definition.
- `Auth/KeycloakTokenProvider.cs`, `Auth/McpAuthHandler.cs`, `Auth/McpAuthOptions.cs` — Keycloak-specific OAuth client-credentials strategy + API-key fallback chain. Library doesn't know Keycloak.
- `Capabilities/QylCapabilityDefinitions.cs`, `Capabilities/QylCapabilityCatalog.cs`, `Capabilities/CapabilityTools.cs` — wrap qyl's generated `QylToolManifest`. Walker stays qyl.
- `Capabilities/QylCapability*Attribute.cs`, `Skills/*` — possibly extractable in a follow-up; not in this slice.
- `Clients/IQylMcpChatClientBuilder.cs`, `Agents/IQylMcpAgentsBuilder.cs` and impls — dropped per user rule "drop unless load-bearing; the chain IS the interface".
- `Apps/{ErrorExplorer,QueryStudio,TraceExplorer}/*` — concrete apps + their HTML.
- All `Tools/**/*` — qyl domain.
- `Metadata/QylServerMetadata.cs` — qyl name/version.
- `Hosting/QylMcpLlmsTextBuilder.cs` — walks qyl-generated manifest.
- `Hosting/QylMcpServerRegistration.cs` — qyl composition root, calls the new library chain.

## Phase plan

| Phase | Who | Duration | Output |
|-------|-----|----------|--------|
| 0 — Bootstrap | Lead | ~10 min | csproj edits in framework + qyl, design spec on disk (this file), stub directories created in framework |
| 1 — Library extraction | 3 parallel teammates | ~25 min | All new files in `src/ANcpLua.Agents.Mcp[.Hosting]/`, behavioral tests in `tests/ANcpLua.Agents.Tests/Hosting/Mcp/`, framework `dotnet build` green |
| 2 — qyl rewire | Lead (single-threaded — same files) | ~20 min | qyl.mcp uses library chain, dead files deleted, `dotnet build qyl.slnx` green |
| 3 — Verify + commit | Lead | ~10 min | Framework + qyl tests pass, two commits (one per repo), evidence captured |

### Phase 1 team partition

Teammate ownership disjoint by directory — zero file conflicts. None touch csproj.

**TM1 `hosting-logging-filters`** (5 files in `.Mcp.Hosting`):
- `Logging/QylMcpLoggingExtensions.cs`
- `Hosting/QylMcpWebHostExtensions.cs`
- `Filters/QylMcpResultSizeFilter.cs`
- `Filters/QylAdminFilterOptions.cs` + `Filters/QylMcpAdminFilter.cs`
- Tests: `tests/ANcpLua.Agents.Tests/Hosting/Mcp/QylMcp{Logging,WebHost,ResultSize,AdminFilter}Tests.cs`

**TM2 `hosting-auth`** (3 files in `.Mcp.Hosting`):
- `Authentication/QylOAuthOptions.cs`
- `Authentication/QylProtectedResourceMetadataOptions.cs`
- `Authentication/QylMcpOAuthExtensions.cs`
- Tests: `tests/ANcpLua.Agents.Tests/Hosting/Mcp/QylMcpOAuthProtectedResourceTests.cs`

**TM3 `protocol-scoping-endpoints`** (3 files split between packages + modify existing):
- `src/ANcpLua.Agents.Mcp/Scoping/IQylConstraintInjector.cs`
- `src/ANcpLua.Agents.Mcp.Hosting/Filters/QylMcpScopeInjectionFilter.cs`
- Modify `src/ANcpLua.Agents.Mcp.Hosting/Facades/QylMcpServerExtensions.cs` — add `mapHealthEndpoints` overload of `MapQylMcp`.
- Tests: `tests/ANcpLua.Agents.Tests/Hosting/Mcp/QylMcp{ScopeInjection,HealthEndpoints}Tests.cs`

## Test pattern

Framework tests use xUnit v3 + AwesomeAssertions. Behavioral shape — see existing `QylStdioMcpClientSmokeTests` as precedent.

Example for `WithAnthropicResultSizeMeta`:

```csharp
[Fact]
public async Task WithAnthropicResultSizeMeta_ResultOverThreshold_SetsMetaKey()
{
    var builder = WebApplication.CreateBuilder();
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithAnthropicResultSizeMeta(thresholdChars: 100)
        .WithTools<LongResponseTool>();

    var app = builder.Build();
    // act via in-memory call
    var result = await InvokeToolAsync(app.Services, "long_response", new());
    
    result.Meta.Should().ContainKey("anthropic/maxResultSizeChars");
    result.Meta["anthropic/maxResultSizeChars"].GetValue<int>().Should().BeGreaterThan(100);
}
```

Tests register **fake tool classes** local to each test file rather than depending on qyl's `QylToolManifest`.

## Sign-off gate

Once this spec is signed off:
- Lead executes Phase 0 (csproj + spec already on disk).
- Lead spawns 3 parallel teammates in one message (single `Agent` invocation block) with `team_name: "qyl-mcp-extract"` and `name: "tm1-hosting-logging-filters"` / `tm2-hosting-auth` / `tm3-protocol-scoping-endpoints`. Each teammate gets the path to THIS spec plus their per-TM file list.
- Lead monitors via `TaskList` + `SendMessage` if a teammate hangs.
- Phase 2 + Phase 3 are lead-only.
