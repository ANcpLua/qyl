# qyl — AI Observability Platform

> **Audience**: AI assistants (Claude, Copilot, Cursor) working in this repository  
> **Goal**: Understand architecture, dependencies, and conventions to make correct changes

## What is qyl?

qyl is an **AI observability backend** that:
- Receives OpenTelemetry (OTLP) telemetry from user applications
- Extracts and indexes `gen_ai.*` semantic convention attributes
- Stores data in embedded DuckDB
- Exposes REST API + SSE streaming for dashboards and AI agents

**qyl is NOT**:
- An OpenTelemetry Collector replacement (no pipelines, no fan-out)
- A custom SDK that users must adopt (they use standard OTel)
- A monolith (dashboard is separately deployable)

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         USER APPLICATIONS                                   │
│                                                                             │
│   Uses Standard OpenTelemetry SDK (NO custom Qyl.Sdk needed!)               │
│   services.AddOpenTelemetry()                                               │
│       .WithTracing(b => b.AddOtlpExporter(o =>                              │
│           o.Endpoint = new Uri("http://qyl:4318")));                        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ OTLP (HTTP :4318 / gRPC :4317)
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         qyl.collector                                       │
│                         (Backend)                                           │
│                                                                             │
│   • OTLP Ingestion (HTTP + gRPC)     • REST API (/api/v1/*)                 │
│   • gen_ai.* Extraction              • SSE Streaming                        │
│   • DuckDB Storage                   • Health endpoints                     │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
             │                                    │
             │ HTTP (REST)                        │ HTTP (REST + SSE)
             ▼                                    ▼
┌─────────────────────┐              ┌─────────────────────┐
│     qyl.mcp         │              │   qyl.dashboard     │
│   (MCP Server)      │              │     (React UI)      │
│                     │              │                     │
│ • AI Agent Tools    │              │ • Sessions View     │
│ • Token Analysis    │              │ • Trace Explorer    │
└─────────────────────┘              └─────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         qyl.protocol                                        │
│                    (Shared Contracts)                                       │
│                                                                             │
│   Primitives: SessionId, UnixNano                                           │
│   Models: SpanRecord, SessionSummary, GenAiSpanData                         │
│   Attributes: GenAiAttributes (OTel 1.38 constants)                         │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
qyl/
├── src/
│   ├── qyl.protocol/        # Shared types (NuGet: Qyl.Protocol)
│   ├── qyl.collector/       # Backend (Docker: ghcr.io/qyl/collector)
│   ├── qyl.mcp/             # MCP Server (Docker: ghcr.io/qyl/mcp)
│   └── qyl.dashboard/       # React UI (Docker: ghcr.io/qyl/dashboard)
├── examples/
├── tests/
├── eng/
│   └── sdk/                 # ANcpLua.NET.Sdk
├── Directory.Build.props    # SDK registration
├── Directory.Packages.props # Central package management
└── global.json
```

## Dependency Rules (ENFORCE STRICTLY)

| Project | MAY Reference | MUST NOT Reference |
|---------|---------------|-------------------|
| `qyl.protocol` | BCL only | Any other qyl.* project |
| `qyl.collector` | qyl.protocol, DuckDB, gRPC, Protobuf | qyl.mcp, qyl.dashboard |
| `qyl.mcp` | qyl.protocol, MCP SDK, HttpClient | qyl.collector (HTTP only!), DuckDB |
| `qyl.dashboard` | React, TanStack Query | Any .NET project |

**Critical**: `qyl.mcp` communicates with `qyl.collector` via HTTP only — NO project reference!

## Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10, C# 14 |
| Web Framework | ASP.NET Core Minimal APIs |
| Storage | DuckDB 1.2.1 |
| Protocol | OTLP (gRPC + HTTP), Protobuf |
| Frontend | React 19, Vite 6, Tailwind 4, TanStack Query 5 |
| MCP | ModelContextProtocol SDK |
| Build | ANcpLua.NET.Sdk (Layering pattern) |

## OpenTelemetry Semantic Conventions

qyl follows **OTel Semantic Conventions v1.38**. Key `gen_ai.*` attributes:

| Attribute | Description |
|-----------|-------------|
| `gen_ai.operation.name` | chat, text_completion, embeddings |
| `gen_ai.provider.name` | anthropic, openai, google (replaces `gen_ai.system`) |
| `gen_ai.request.model` | Model requested (e.g., gpt-4o) |
| `gen_ai.response.model` | Model that responded |
| `gen_ai.usage.input_tokens` | Prompt tokens (replaces `prompt_tokens`) |
| `gen_ai.usage.output_tokens` | Completion tokens (replaces `completion_tokens`) |

**Migration**: Collector normalizes deprecated attributes to v1.38 format.

## Build System

Uses **ANcpLua.NET.Sdk** with layering pattern:

```xml
<!-- Directory.Build.props -->
<Project>
  <Sdk Name="ANcpLua.NET.Sdk"/>
</Project>

<!-- Individual .csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <!-- SDK auto-detects Web and adds OTel, health checks, etc. -->
</Project>
```

## Anti-Patterns (REJECT)

| ❌ Anti-Pattern | ✅ Use Instead |
|----------------|----------------|
| `DateTime.Now` | `TimeProvider.System.GetLocalNow()` |
| `DateTime.UtcNow` | `TimeProvider.System.GetUtcNow()` |
| `new object()` for locks | `new Lock()` (.NET 9+) |
| `Thread.Sleep()` | `await Task.Delay()` |
| `new HttpClient()` | `IHttpClientFactory` |
| `Task.Result` / `.Wait()` | `await` |
| `Newtonsoft.Json` | `System.Text.Json` |
| `gen_ai.system` | `gen_ai.provider.name` |
| `gen_ai.usage.prompt_tokens` | `gen_ai.usage.input_tokens` |

## Required Patterns

| Pattern | Usage |
|---------|-------|
| `Lock` | Thread-safe synchronization (.NET 9+) |
| `FrozenSet<T>` | Immutable lookup sets |
| `FrozenDictionary<K,V>` | Immutable config dictionaries |
| `SearchValues<T>` | SIMD-optimized membership tests |
| `TypedResults.ServerSentEvents()` | SSE in .NET 10 |
| `HybridCache` | Stampede-proof caching |

## Subproject Documentation

@src/qyl.protocol/CLAUDE.md
@src/qyl.collector/CLAUDE.md
@src/qyl.mcp/CLAUDE.md
@src/qyl.dashboard/CLAUDE.md

## Quick Reference

### Ports
- `8080` — REST API + SSE
- `4317` — OTLP gRPC
- `4318` — OTLP HTTP

### Key Endpoints
- `POST /v1/traces` — OTLP HTTP ingestion
- `GET /api/v1/sessions` — List sessions
- `GET /api/v1/spans?genAiOnly=true` — Query spans
- `GET /api/v1/events/spans` — SSE stream

### Commands
```bash
# Build
dotnet build

# Run collector
dotnet run --project src/qyl.collector

# Run tests
dotnet test

# Run dashboard
cd src/qyl.dashboard && pnpm dev
```
