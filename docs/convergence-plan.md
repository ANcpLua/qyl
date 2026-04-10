# Convergence Plan: AOT MCP Server for Claude

**Date:** 2026-04-08
**Scope:** Unify netagents (Qyl.Agents) and qyl.mcp generator stacks into a single compile-time MCP server framework that is Claude-native, AOT-first, and OTel-correct.

---

## Current State Map

### Three stacks, two generators, zero shared code

| Dimension | netagents (Qyl.Agents) | qyl.mcp (ToolManifestGenerator) | Official C# SDK (v1.2.0) |
|-----------|------------------------|--------------------------------|--------------------------|
| **Trigger** | `[McpServer]` + `[Tool]` (custom) | `[McpServerToolType]` + `[McpServerTool]` (official SDK) | `[McpServerToolType]` + `[McpServerTool]` |
| **Code gen** | Full: dispatch, schema, OTel, JSON context, SKILL.md, llms.txt | Minimal: type array + delegate factory | None (runtime reflection) |
| **Transport** | stdio only | stdio + Streamable HTTP | stdio + Streamable HTTP |
| **Auth** | None | JWT, Keycloak, OAuth2 | OAuth 2.1 + DCR |
| **OTel** | Per-tool spans + metrics (bugs: deprecated `gen_ai.system`, invalid `server.name`) | Host-level message/request filters | None built-in |
| **AOT** | Strong (primitives direct, complex type warnings) | Partial (AIFunctionFactory uses reflection) | Supported (`PublishAot=true`) |
| **DI** | None (instance-based) | Full (IServiceProvider) | Full (IServiceProvider) |
| **Resources** | Yes (`[Resource]`) | No | Yes |
| **Prompts** | Yes (`[Prompt]`) | No | Yes |
| **Tasks** | Yes (`IMcpTaskStore`, MCPEXP001) | No | Experimental |
| **Validation** | Comprehensive (types, nesting, duplicates, diagnostics) | Minimal (syntax only) | Runtime |
| **Consumers** | qyl.loom (1 server, 3 tools) | qyl.mcp (77 tool files) | MAF samples, ecosystem |

### What Datzi sees

Two systems. Zero shared code. netagents has the better compiler. qyl.mcp has the better host. Neither alone is Claude-ready.

---

## Anthropic Constraints (non-negotiable)

These come from official Anthropic docs, the MCP spec (2025-11-25), and the C# SDK (v1.2.0).

### Transport

| Claude client | stdio | Streamable HTTP | SSE (deprecated) |
|---------------|-------|-----------------|-------------------|
| Claude Code | Yes | Yes | Yes (legacy) |
| Claude Desktop | Yes (local) | Yes (Connectors) | Yes (legacy) |
| Claude Web UI | **No** | **Yes (required)** | Yes (legacy) |
| Messages API (MCP Connector) | **No** | **Yes (required)** | Yes (legacy) |
| Claude iOS/Android | **No** | **Yes (inherited)** | Yes (legacy) |

**Implication:** stdio-only servers (current netagents) cannot reach Claude Web UI, the Messages API, or mobile. Streamable HTTP is mandatory for any production MCP server.

### Authentication

- OAuth 2.1 with Dynamic Client Registration (DCR) is the standard
- OAuth metadata discovery: RFC 9728 (`.well-known/oauth-protected-resource`) first, RFC 8414 fallback
- Bearer tokens via `authorization_token` field in server config
- OAuth callback: `https://claude.ai/api/mcp/auth_callback`
- All remote connections originate from Anthropic's cloud infrastructure

### Tool Design (Claude-native UX)

| Rule | Detail |
|------|--------|
| **Names** | `^[a-zA-Z0-9_-]{1,64}$}`. Prefix with domain (e.g. `qyl_search_traces`). MCP surfaces as `mcp__<server>__<tool>` |
| **Descriptions** | 3-4+ sentences. What it does, when to use it, when NOT to use it, parameter meanings, caveats. **Single most important factor for Claude tool selection quality** |
| **Schemas** | JSON Schema with `description` on every property. Use `enum` for constrained values. Mark required vs optional explicitly |
| **Input examples** | `input_examples` field (array) for complex/nested tools |
| **Strict mode** | `strict: true` guarantees Claude's calls always match schema |
| **Structured output** | `outputSchema` (JSON Schema) + `structuredContent` field. Must still include `text` for backward compat |
| **Errors** | Return `is_error: true` with actionable message. Never hang. Never crash |
| **Responses** | High-signal only. Stable identifiers (slugs, UUIDs). Only fields Claude needs for next reasoning step |

### API Connector Limitations

The Messages API MCP Connector (beta header `mcp-client-2025-11-20`) **only supports tools**. No prompts. No resources. Servers targeting the API connector must design around tools only.

### SSE Deprecation

C# SDK v1.2.0 disabled legacy SSE endpoints by default. `MapMcp()` no longer maps `/sse` and `/message`. Opt-in via `EnableLegacySse = true` (marked `[Obsolete]`).

Sources:
- [Build custom connectors](https://support.claude.com/en/articles/11503834-build-custom-connectors-via-remote-mcp-servers)
- [MCP connector API docs](https://platform.claude.com/docs/en/agents-and-tools/mcp-connector)
- [Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools)
- [C# SDK v1.2.0](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v1.2.0)

---

## OTel Semantic Conventions (non-negotiable, no shims)

All conventions below are current as of OTel semconv 1.40+. The MCP-specific conventions were added in early 2026 (PR #2083). No shims, no custom attributes where a convention exists.

### MCP Server Span (transport level)

Span kind: `SERVER`. Span name: `{mcp.method.name} {target}` (e.g. `tools/call get_weather`).

| Attribute | Type | Req | Stability |
|-----------|------|-----|-----------|
| `mcp.method.name` | string | Required | Development |
| `error.type` | string | Cond. Required (on error) | Stable |
| `gen_ai.tool.name` | string | Cond. Required | Development |
| `gen_ai.prompt.name` | string | Cond. Required | Development |
| `jsonrpc.request.id` | string | Cond. Required | Development |
| `mcp.resource.uri` | string | Cond. Required | Development |
| `rpc.response.status_code` | string | Cond. Required | Release Candidate |
| `gen_ai.operation.name` | string | Recommended | Development |
| `mcp.protocol.version` | string | Recommended | Development |
| `mcp.session.id` | string | Recommended | Development |
| `jsonrpc.protocol.version` | string | Recommended | Development |
| `network.protocol.name` | string | Recommended | Stable |
| `network.transport` | string | Recommended | Stable |
| `client.address` | string | Recommended | Stable |
| `gen_ai.tool.call.arguments` | any | Opt-In | Development |
| `gen_ai.tool.call.result` | any | Opt-In | Development |

### GenAI Tool Execution Span (business logic level)

Span kind: `INTERNAL`. Span name: `execute_tool {gen_ai.tool.name}`.

| Attribute | Type | Req | Stability |
|-----------|------|-----|-----------|
| `gen_ai.operation.name` | string | Required (`execute_tool`) | Development |
| `error.type` | string | Cond. Required (on error) | Stable |
| `gen_ai.tool.name` | string | Recommended | Development |
| `gen_ai.tool.type` | string | Recommended (`function`) | Development |
| `gen_ai.tool.call.id` | string | Recommended | Development |
| `gen_ai.tool.description` | string | Recommended | Development |

### Metrics

| Metric | Instrument | Unit | Level |
|--------|-----------|------|-------|
| `mcp.server.operation.duration` | Histogram | `s` | Transport |
| `mcp.server.session.duration` | Histogram | `s` | Transport |
| `gen_ai.client.operation.duration` | Histogram | `s` | Tool execution |

MCP bucket boundaries: `[0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60, 120, 300]`
GenAI bucket boundaries: `[0.01, 0.02, 0.04, 0.08, 0.16, 0.32, 0.64, 1.28, 2.56, 5.12, 10.24, 20.48, 40.96, 81.92]`

### Recommended Span Hierarchy

```
HTTP SERVER span (auto, ASP.NET Core Kestrel — do NOT emit manually)
  └─ MCP SERVER span: "tools/call get_weather"
       ├─ mcp.method.name = "tools/call"
       ├─ gen_ai.tool.name = "get_weather"
       ├─ gen_ai.operation.name = "execute_tool"
       ├─ mcp.protocol.version = "2025-06-18"
       ├─ jsonrpc.request.id = "42"
       ├─ jsonrpc.protocol.version = "2.0"
       └─ INTERNAL span: "execute_tool get_weather"
            ├─ gen_ai.operation.name = "execute_tool"
            ├─ gen_ai.tool.name = "get_weather"
            ├─ gen_ai.tool.type = "function"
            ├─ gen_ai.tool.call.id = "call_abc123"
            └─ error.type (only on failure)
```

### Context Propagation

Inject/extract W3C Trace Context (`traceparent`, `tracestate`) and Baggage via MCP `params._meta` property bag.

### Bugs in Current netagents OTel

| File | Line | Issue | Fix |
|------|------|-------|-----|
| `DispatchEmitter.cs` | 70 | Emits `gen_ai.system` — **deprecated** | Remove entirely. `gen_ai.provider.name` is not applicable to execute_tool spans |
| `DispatchEmitter.cs` | 71 | Emits `server.name` — **not a semconv attribute** | Remove entirely |
| `DispatchEmitter.cs` | — | Missing `gen_ai.tool.call.id` | Add (Recommended) |
| `DispatchEmitter.cs` | — | Missing `gen_ai.tool.description` | Add (Recommended) |
| `McpProtocolHandler.cs` | — | Missing `mcp.protocol.version` | Add (Recommended) |
| `McpProtocolHandler.cs` | — | Missing `mcp.session.id` | Add (Recommended) |
| `McpProtocolHandler.cs` | — | Missing `jsonrpc.protocol.version` | Add (Recommended) |
| `McpProtocolHandler.cs` | — | Missing `rpc.response.status_code` on errors | Add (Cond. Required) |
| `OTelEmitter.cs` | — | Only emits `gen_ai.client.operation.duration` | Also emit `mcp.server.operation.duration` |

Sources:
- [MCP Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/mcp/)
- [MCP Registry Attributes](https://opentelemetry.io/docs/specs/semconv/registry/attributes/mcp/)
- [GenAI Spans](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/)
- [MCP Conventions PR #2083](https://github.com/open-telemetry/semantic-conventions/pull/2083)

---

## Generator Comparison: What Each Does Uniquely

### netagents does (not in qyl.mcp)

1. **Source-generated dispatch** — `DispatchToolCallAsync()` with switch expression routing by tool name, per-tool private methods, direct `JsonElement` accessors for primitives
2. **Source-generated JSON schemas** — static `byte[]` arrays, compile-time construction, zero runtime cost
3. **Source-generated JSON contexts** — `[JsonSourceGenerationOptions]` partial class, only when complex types exist
4. **Per-tool OTel spans** — ActivitySource + Histogram emitted in generated code (bugs noted above)
5. **Resource endpoints** — `[Resource]` methods for content reads
6. **Prompt templates** — `[Prompt]` methods with parameter extraction
7. **SKILL.md + llms.txt generation** — agent platform metadata
8. **Comprehensive validation** — 10+ diagnostics (QA0003–QA0010), parameter type checking, duplicate detection, nesting validation
9. **Task support** — `IMcpTaskStore` for long-running operations (MCPEXP001)
10. **Tool hints** — `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld` as `ToolHint` enum with Unset/True/False semantics

### qyl.mcp does (not in netagents)

1. **Streamable HTTP transport** — full ASP.NET Core hosting with `MapMcp()`
2. **Authentication** — JWT bearer, Keycloak, OAuth2 middleware
3. **Message filtering** — incoming/outgoing JSON-RPC middleware pipeline
4. **Request filtering** — per-tool access control, scope injection, rate limiting
5. **Health checks** — `/healthz` endpoint
6. **Landing page** — HTML + JSON server discovery endpoint
7. **Tool filtering** — `Func<Type, bool>?` predicate in `CreateTools()`
8. **Full DI** — `IServiceProvider.GetRequiredService<T>()` per tool type
9. **Manifest endpoints** — `/mcp.json` and `/llms.txt` via HTTP routes
10. **Host-level OTel** — transport-layer instrumentation via SDK message/request filters

### Where they overlap

- Both use `IIncrementalGenerator` + `ForAttributeWithMetadataName`
- Both produce value-equatable models for incremental caching
- Both emit a single generated file per compilation
- Both avoid runtime type discovery for tool registration
- Both target .NET 10 / C# 14

---

## Architecture: Unified AOT MCP Server

### Design Principles

1. **Compile-time everything** — schemas, dispatch, OTel, JSON contexts generated at build. Zero runtime reflection.
2. **Claude-native first** — tool descriptions enforced, structured output supported, Anthropic transport requirements met.
3. **OTel-correct** — MCP semconv 2026, no deprecated attributes, no custom attributes where conventions exist.
4. **Official SDK as substrate** — use `ModelContextProtocol` NuGet for transport/hosting. Generate the tool surface, don't replace the host.
5. **AOT by default** — `PublishAot=true` must work without warnings. Source-generated JSON for all serialization paths.

### The Key Insight

netagents should **not** replace the official SDK's hosting. It should **generate the tool implementations** that plug into the official SDK's host.

```
                    ┌──────────────────────────────────────┐
                    │   Official C# MCP SDK (v1.2.0)       │
                    │   ─ Streamable HTTP transport         │
                    │   ─ stdio transport                   │
                    │   ─ OAuth 2.1 + DCR                   │
                    │   ─ Session management                │
                    │   ─ MapMcp() ASP.NET Core             │
                    └───────────────┬──────────────────────┘
                                    │ consumes
                    ┌───────────────▼──────────────────────┐
                    │   Qyl.Agents.Generator (compile-time) │
                    │   ─ [McpServer] + [Tool] attributes   │
                    │   ─ Source-generated McpServerTool[]   │
                    │   ─ Source-generated JSON schemas      │
                    │   ─ Source-generated dispatch          │
                    │   ─ Source-generated OTel (MCP semconv)│
                    │   ─ Source-generated JSON contexts     │
                    │   ─ Validation diagnostics             │
                    └───────────────┬──────────────────────┘
                                    │ implements
                    ┌───────────────▼──────────────────────┐
                    │   User's MCP Server Class              │
                    │                                        │
                    │   [McpServer("qyl-telemetry")]          │
                    │   partial class QylTelemetryServer      │
                    │   {                                     │
                    │     [Tool] SearchTraces(...)             │
                    │     [Tool] GetSpan(...)                  │
                    │     [Resource] GetDashboard(...)         │
                    │   }                                     │
                    └────────────────────────────────────────┘
```

### Generated Output: What Changes

**Current** (netagents generates its own `IMcpServer` interface + `McpHost` runtime):
```csharp
// Generated: implements custom IMcpServer
partial class MyServer : IMcpServer
{
    public Task<string> DispatchToolCallAsync(string toolName, JsonElement args, CancellationToken ct)
        => toolName switch { "my_tool" => ExecuteTool_MyToolAsync(args, ct), _ => throw ... };
}

// Runtime: custom McpHost reads stdin, writes stdout
await McpHost.RunStdioAsync(new MyServer());
```

**Target** (netagents generates `McpServerTool[]` that plug into official SDK):
```csharp
// Generated: source-generated McpServerTool instances (no reflection)
partial class MyServer
{
    public static McpServerTool[] CreateMcpTools(IServiceProvider services)
    {
        var instance = services.GetRequiredService<MyServer>();
        return
        [
            McpServerTool.Create(
                name: "my_tool",
                description: "Does the thing. Use when you need X. Do NOT use for Y.",
                inputSchema: s_schema_MyTool, // static byte[], compile-time
                handler: (args, ct) => instance.DispatchTool_MyToolAsync(args, ct),
                inputSchemaStrict: true,
                readOnly: true,
                idempotent: true),
        ];
    }

    // Generated: AOT-safe dispatch with direct JsonElement accessors
    private async Task<McpToolResponse> DispatchTool_MyToolAsync(
        JsonElement args, CancellationToken ct)
    {
        using var activity = s_activitySource.StartActivity(
            "execute_tool my_tool", ActivityKind.Internal);
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.name", "my_tool");
        activity?.SetTag("gen_ai.tool.type", "function");

        var param1 = args.GetProperty("query").GetString()!;
        var result = await MyTool(param1, ct);
        return McpToolResponse.Success(result);
    }
}

// Hosting: official SDK does transport
var builder = Host.CreateApplicationBuilder();
builder.Services.AddSingleton<MyServer>();
builder.Services.AddMcpServer()
    .WithStreamableHttpTransport()
    .WithTools(MyServer.CreateMcpTools);
await builder.Build().RunAsync();
```

### What the Generator Emits (per server class)

| Output | Purpose | AOT-safe |
|--------|---------|----------|
| `CreateMcpTools(IServiceProvider)` | Static factory returning `McpServerTool[]` | Yes — no reflection |
| `DispatchTool_{Name}Async(JsonElement, CancellationToken)` | Per-tool dispatch with direct `JsonElement` accessors | Yes — primitives direct, complex via source-gen JSON |
| `s_schema_{Name}` | Static `byte[]` JSON Schema per tool | Yes — compile-time |
| `{ClassName}JsonContext` | `[JsonSourceGenerationOptions]` partial context | Yes — source-gen JSON |
| `s_activitySource` | `ActivitySource("Qyl.Agents")` | Yes |
| `s_mcpDuration` | `Histogram<double>("mcp.server.operation.duration")` | Yes |
| `s_toolDuration` | `Histogram<double>("gen_ai.client.operation.duration")` | Yes |
| `GetServerInfo()` | Static metadata for server discovery | Yes |
| `GetSkillMarkdown()` | SKILL.md content as string | Yes |
| `GetLlmsTxt()` | llms.txt content as string | Yes |

### OTel Emission (corrected)

**Transport-level span** (emitted in protocol handler, not per-tool):
```csharp
using var activity = s_activitySource.StartActivity(
    $"{mcpMethod} {target}", ActivityKind.Server);
activity?.SetTag("mcp.method.name", mcpMethod);           // Required
activity?.SetTag("mcp.protocol.version", "2025-06-18");    // Recommended
activity?.SetTag("mcp.session.id", sessionId);             // Recommended
activity?.SetTag("jsonrpc.request.id", requestId);         // Cond. Required
activity?.SetTag("jsonrpc.protocol.version", "2.0");       // Recommended
// On tools/call:
activity?.SetTag("gen_ai.tool.name", toolName);            // Cond. Required
activity?.SetTag("gen_ai.operation.name", "execute_tool");  // Recommended
// On error:
activity?.SetTag("error.type", exception.GetType().Name);  // Cond. Required
activity?.SetTag("rpc.response.status_code", errorCode);   // Cond. Required
```

**Tool-level span** (emitted in generated dispatch):
```csharp
using var activity = s_activitySource.StartActivity(
    $"execute_tool {toolName}", ActivityKind.Internal);
activity?.SetTag("gen_ai.operation.name", "execute_tool");  // Required
activity?.SetTag("gen_ai.tool.name", toolName);             // Recommended
activity?.SetTag("gen_ai.tool.type", "function");           // Recommended
activity?.SetTag("gen_ai.tool.call.id", callId);            // Recommended
activity?.SetTag("gen_ai.tool.description", description);   // Recommended
// NO gen_ai.system (deprecated)
// NO gen_ai.provider.name (not applicable to tool execution)
// NO server.name (not a semconv attribute)
```

### Validation Diagnostics

| ID | Severity | Message |
|----|----------|---------|
| QA0001 | Error | `[McpServer]` class must be partial |
| QA0002 | Error | `[McpServer]` class must not be static or generic |
| QA0003 | Warning | Tool has no hints set (ReadOnly, Destructive, Idempotent, OpenWorld) |
| QA0004 | Error | `[Tool]` method found outside `[McpServer]` class |
| QA0005 | Error | Duplicate tool name in server |
| QA0006 | Error | Duplicate resource URI in server |
| QA0007 | Error | Duplicate prompt name in server |
| QA0008 | Warning | Parameter missing `[Description]` attribute |
| QA0009 | Error | Tool method must not be static or generic |
| QA0010 | Error | Unsupported parameter type |
| **QA0011** | **Warning** | **Tool description is fewer than 50 characters** (new — Claude quality) |
| **QA0012** | **Warning** | **Parameter description missing or fewer than 10 characters** (new — Claude quality) |
| **QA0013** | **Info** | **Consider adding `input_examples` for complex tool schemas** (new — Claude quality) |

---

## What This Means for Each Project

### For qyl.mcp (77 tool files)

**No immediate migration required.** qyl.mcp's `ToolManifestGenerator` + official SDK hosting works. The path is:

1. **Phase 1:** Fix OTel in qyl.mcp's request filters to use MCP semconv attributes (add `mcp.method.name`, `mcp.protocol.version`, etc.)
2. **Phase 2:** When netagents v0.3.0 ships with the unified generator, qyl.mcp can optionally migrate tool classes from `[McpServerToolType]`/`[McpServerTool]` to `[McpServer]`/`[Tool]` to gain compile-time schemas, per-tool OTel, and AOT-safe dispatch
3. **Phase 3:** qyl.mcp's custom `ToolManifestGenerator` becomes unnecessary — netagents generates the same output but with more features

The migration is **incremental**. One tool class at a time. Both generators can coexist during transition.

### For qyl.loom (1 server, 3 tools)

**Already on netagents.** Benefits from all fixes immediately:

1. OTel attributes corrected (remove deprecated `gen_ai.system`, add MCP semconv)
2. Streamable HTTP transport (currently stdio-only via `McpHost`)
3. OAuth 2.1 support for remote access from Claude Web UI

### For netagents itself

**This is the work:**

1. Fix OTel bugs in `DispatchEmitter.cs` and `OTelEmitter.cs`
2. Generate `McpServerTool[]` factory method instead of (or alongside) custom `IMcpServer`
3. Add HTTP hosting integration (`McpEndpoints.MapMcpServer<T>()` for ASP.NET Core)
4. Add Claude-quality diagnostics (QA0011, QA0012, QA0013)
5. Add structured output schema support (`outputSchema` + `structuredContent`)
6. Keep `McpHost.RunStdioAsync()` for backward compat and local dev

---

## Ecosystem Fit

### Where netagents sits vs MAF vs official SDK

From the MAF MCP HITS analysis ([src.md](src.md), [sample.md](sample.md)):

- **MAF** is strongest at orchestration, transport, hosted execution, and protocol bridging. It is not strongest at compile-time server authoring.
- **Official C# SDK** is the canonical transport/server substrate. Stable, AOT-compatible, maintained by Microsoft.
- **netagents** is the compile-time authoring layer. It generates what the official SDK consumes.

These are complementary, not competing:

```
MAF (orchestration) ──consumes──▶ MCP servers
Official SDK (transport) ──hosts──▶ MCP servers
netagents (authoring) ──generates──▶ MCP server tools
```

### Anthropic ecosystem alignment

| Anthropic priority | netagents alignment |
|-------------------|---------------------|
| Streamable HTTP | Phase 2 (via official SDK integration) |
| OAuth 2.1 + DCR | Phase 2 (via ASP.NET Core middleware) |
| Tool descriptions | Phase 1 (QA0011 diagnostic enforces 50+ char descriptions) |
| Structured output | Phase 2 (generate `outputSchema` from return type) |
| AOT | Already strong, gets stronger with generated `McpServerTool[]` factory |
| OTel | Phase 1 (fix deprecated attributes, add MCP semconv) |

### What "Claude-native" means in practice

Not "works with Claude." Every MCP server works with Claude. "Claude-native" means:

1. **Tool descriptions read like documentation** — Claude selects tools based on descriptions, not names. 3-4 sentences minimum. Enforce at compile time.
2. **Structured output schemas** — Claude can return structured data validated against server-declared schemas. Generate these from C# return types.
3. **Error responses are actionable** — `is_error: true` with "what went wrong, what to do next." Generate error wrapping in dispatch.
4. **Streaming where it matters** — long-running tools use Tasks (MCPEXP001) so Claude doesn't time out waiting.
5. **Transport negotiation** — Streamable HTTP for remote, stdio for local. Same server binary, different host configuration.
6. **OAuth 2.1** — Claude Web UI and API connector authenticate via DCR. Not optional for remote servers.

---

## Summary

| What | Status | Action |
|------|--------|--------|
| netagents generator | Strong foundation, bugs in OTel | Fix OTel, add `McpServerTool[]` factory, add Claude diagnostics |
| netagents runtime | stdio-only, custom protocol | Keep for local dev. Add official SDK integration for HTTP |
| qyl.mcp generator | Works but minimal | Keep until netagents can replace it. Then migrate incrementally |
| qyl.mcp hosting | Production-ready | No change. Official SDK + ASP.NET Core stays |
| qyl.loom | Already on netagents | Benefits from all fixes. Add HTTP transport for Claude Web UI |
| OTel | Bugs: deprecated attributes, missing MCP semconv | Fix in Phase 1. No shims, no custom attributes |
| AOT | Strong for primitives | Full AOT with source-generated JSON contexts |
| Claude UX | Not enforced | Phase 1: diagnostics. Phase 2: structured output |

One generator. Official SDK for transport. OTel-correct. Claude-native. AOT by default. No shims.
