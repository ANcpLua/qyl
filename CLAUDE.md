# qyl — AI Observability Platform

Architecture & Requirements Specification (NASA-style). This file is the SINGLE SOURCE OF TRUTH for qyl system
architecture.

Last Updated: 2025-12-13  
Maintainer: @ANcpLua

> .NET 10 · C# 14 · OpenTelemetry SemConv 1.38 · DuckDB · Native AOT · REST/SSE · MCP · React

## Document Structure

| Section                   | Explains                                                 |
|---------------------------|----------------------------------------------------------|
| System Architecture       | ASCII diagram, exactly 4 components                      |
| Dependency Graph          | Who may reference whom (HTTP-only rules)                 |
| CLAUDE.md Inheritance     | `@import` chain, per-project context                     |
| SDK Injection Features    | `<InjectSharedThrow>`, `<InjectClaudeBrain>`, polyfills  |
| Code Quality              | `BannedSymbols.txt`, analyzers, required/banned patterns |
| OTel Semantic Conventions | v1.38 `gen_ai.*` keys + migration rules                  |
| API Specification         | OTLP + REST + SSE endpoints                              |
| Rejected Alternatives     | Explicit “no” decisions with rationale                   |
| Single Source Rules       | File-level “only place this may live” rules              |

## Meta

```yaml
meta:
  name: qyl
  full_name: "qyl AI Observability Platform"
  description: |
    Backend system that receives OpenTelemetry telemetry, extracts gen_ai.*
    semantic convention attributes, stores in DuckDB, and exposes REST/SSE APIs
    for dashboards and AI agents via MCP.
  version: 1.0.0
  target_framework: net10.0
  language_version: C# 14
  otel_semconv_version: 1.38.0
```

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              USER APPLICATIONS                               │
│                                                                             │
│  Uses standard OpenTelemetry SDK (NO custom qyl client SDK required)         │
│  services.AddOpenTelemetry()                                                 │
│      .WithTracing(b => b.AddOtlpExporter(o =>                                │
│          o.Endpoint = new Uri("http://qyl-collector:4318")));                │
└─────────────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ OTLP (HTTP :4318 / gRPC :4317)
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                               qyl.collector                                  │
│                         (Backend · Native AOT)                               │
│                                                                             │
│  • OTLP ingestion (HTTP + gRPC)     • REST API (/api/v1/*)                   │
│  • gen_ai.* extraction              • SSE streaming                          │
│  • DuckDB storage                   • Health endpoints                       │
└─────────────────────────────────────────────────────────────────────────────┘
             │                                     │
             │ HTTP (REST)                         │ HTTP (REST + SSE)
             ▼                                     ▼
┌─────────────────────┐               ┌─────────────────────┐
│       qyl.mcp        │               │    qyl.dashboard     │
│     (MCP server)     │               │     (React UI)       │
│  stdio transport      │               │  REST + SSE client   │
└─────────────────────┘               └─────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                               qyl.protocol                                   │
│                          (Shared Contracts · LEAF)                           │
│                                                                             │
│  Primitives: SessionId, UnixNano                                             │
│  Models: SpanRecord, SessionSummary, GenAiSpanData, TraceNode                │
│  Attributes: GenAiAttributes (OTel 1.38 constants)                           │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Project Structure

Exactly 4 system components exist. New runtime components are NOT allowed without updating this document.

```yaml
projects:
  count: 4

  qyl.protocol:
    path: src/qyl.protocol
    type: class_library
    purpose: "Shared types between all qyl components (LEAF)"
    target_frameworks: [net10.0, net8.0, netstandard2.0]

  qyl.collector:
    path: src/qyl.collector
    type: web_api
    purpose: "Backend: OTLP receiver, DuckDB storage, REST/SSE"
    ports: { http_api: 5100, otlp_http: 4318, otlp_grpc: 4317 }

  qyl.mcp:
    path: src/qyl.mcp
    type: console_app
    purpose: "MCP server: talks to collector via HTTP only (no DB access)"
    transport: stdio

  qyl.dashboard:
    path: src/qyl.dashboard
    type: spa
    purpose: "React frontend: talks to collector via HTTP (REST + SSE)"
```

Tooling/test projects may exist under `eng/`, `examples/`, `tests/`; they MUST NOT be depended on by the 4 system
components.

## Dependency Graph

```
qyl.dashboard ──HTTP──► qyl.collector ◄──HTTP── qyl.mcp
                               │
                               ▼
                         qyl.protocol
```

Rules:

- `qyl.protocol` is LEAF (references nothing outside BCL)
- `qyl.collector` may reference `qyl.protocol`
- `qyl.mcp` may reference `qyl.protocol` and communicates with collector via HTTP ONLY (no `ProjectReference` to
  collector)
- `qyl.dashboard` references NO .NET projects (HTTP only)

## CLAUDE.md Inheritance

Each project has a `CLAUDE.md` file that inherits from root via a single import line at the top:

```text
 @import "../../CLAUDE.md"
```

File tree:

```text
/CLAUDE.md
/src/qyl.protocol/CLAUDE.md
/src/qyl.collector/CLAUDE.md
/src/qyl.mcp/CLAUDE.md
/src/qyl.dashboard/CLAUDE.md
```

## Build System

- Solution format: `qyl.slnx` (XML, merge-friendly, see Microsoft SolutionPersistence "slnx")
- Repo-wide build defaults: `Directory.Build.props`, `Directory.Build.targets`

## TODO

- [ ] Migrate to `<Project Sdk="ANcpLua.NET.Sdk/1.0.0">` (published on NuGet)
- [ ] Remove `eng/MSBuild/` after SDK migration
- [ ] Update projects to use SDK's BannedApiAnalyzers, polyfills, and CLAUDE.md generation

## SDK Injection Features

```yaml
sdk_injection_features:
  InjectSharedThrow:
    property: "<InjectSharedThrow>true</InjectSharedThrow>"
    implementation: "eng/MSBuild/Shared.targets (links src/Shared/Throw/**/*.cs)"

  InjectCallerAttributesOnLegacy:
    property: "<InjectCallerAttributesOnLegacy>true</InjectCallerAttributesOnLegacy>"
    implementation: "eng/MSBuild/LegacySupport.targets"

  InjectDiagnosticAttributesOnLegacy:
    property: "<InjectDiagnosticAttributesOnLegacy>true</InjectDiagnosticAttributesOnLegacy>"
    implementation: "eng/MSBuild/LegacySupport.targets"

  InjectIsExternalInitOnLegacy:
    property: "<InjectIsExternalInitOnLegacy>true</InjectIsExternalInitOnLegacy>"
    implementation: "eng/MSBuild/LegacySupport.targets"

  InjectClaudeBrain:
    property: "<InjectClaudeBrain>true</InjectClaudeBrain>"
    status: planned
    note: "Auto-generate per-project CLAUDE.md with correct @import and metadata"
```

## Code Quality

- Banned APIs enforced via `Microsoft.CodeAnalysis.BannedApiAnalyzers` + `eng/MSBuild/BannedSymbols.txt` (RS0030)
- Analyzer baseline: `.editorconfig` + `Directory.Build.props` (CI: treat selected warnings as errors)

Required patterns:

- Threading: `.NET Lock` (not `lock(object)`, not `Monitor.*`)
- Time: `TimeProvider` (not `DateTime.UtcNow/Now/Today`)
- Collections: `FrozenSet<T>`, `FrozenDictionary<K,V>` for static lookups
- Streaming: `IAsyncEnumerable<T>` and (collector) `TypedResults.ServerSentEvents`

Naming conventions (
per [Microsoft .NET Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines)):

| Identifier Type         | Convention    | Example          |
|-------------------------|---------------|------------------|
| Constants (`const`)     | PascalCase    | `MaxBatchSize`   |
| Static readonly fields  | PascalCase    | `DefaultOptions` |
| Private instance fields | `_camelCase`  | `_connection`    |
| Private static fields   | `s_camelCase` | `s_counter`      |
| Public/Internal fields  | PascalCase    | `DefaultTimeout` |

## OTel Semantic Conventions (GenAI v1.38)

Current keys (selected):

- `gen_ai.operation.name` (`chat`, `text_completion`, `embeddings`, `image_generation`)
- `gen_ai.provider.name` (replaces deprecated `gen_ai.system`)
- `gen_ai.request.model`, `gen_ai.response.model`
- `gen_ai.usage.input_tokens` (replaces `gen_ai.usage.prompt_tokens`)
- `gen_ai.usage.output_tokens` (replaces `gen_ai.usage.completion_tokens`)

Migrations (MUST normalize on ingest):

```yaml
migrations:
  - from: gen_ai.system
    to: gen_ai.provider.name
  - from: gen_ai.usage.prompt_tokens
    to: gen_ai.usage.input_tokens
  - from: gen_ai.usage.completion_tokens
    to: gen_ai.usage.output_tokens
```

## API Specification

Base URL (local dev): `http://localhost:5100`

OTLP ingestion:

- HTTP: `POST /v1/traces` (port `4318`, `application/x-protobuf`)
- gRPC: `opentelemetry.proto.collector.trace.v1.TraceService/Export` (port `4317`)

REST:

- `GET /api/v1/sessions?limit={n}`
- `GET /api/v1/sessions/{sessionId}`
- `GET /api/v1/traces/{traceId}`
- `GET /api/v1/spans?serviceName=&from=&to=&genAiOnly=&limit=`

Streaming:

- SSE: `GET /api/v1/events/spans` (event type: `span`, data: `SpanRecord` JSON)

Health:

- `GET /health` → `{ "status": "healthy" }`

## Rejected Alternatives

- ZLinq: DuckDB I/O is the bottleneck, not LINQ overhead
- Separate SDK variants (Web/Test/etc.): one layered SDK is simpler (auto-detect)
- Custom TraceId/SpanId structs: `ActivityTraceId/ActivitySpanId` are sufficient
- “qyl client SDK” for user apps: users should use standard OpenTelemetry

## Single Source Rules

If you change one of these concerns, change ONLY the source file listed here:

| Concern              | Single Source                                                     |
|----------------------|-------------------------------------------------------------------|
| DuckDB schema        | `src/qyl.collector/Storage/DuckDbSchema.cs`                       |
| Session aggregation  | `src/qyl.collector/Query/SessionQueryService.cs`                  |
| OTel GenAI constants | `src/qyl.protocol/Attributes/GenAiAttributes.cs`                  |
| Shared guard clauses | `src/Shared/Throw/Throw.cs` (via MSBuild injection)               |
| Dashboard API types  | `src/qyl.dashboard/src/types/generated/` (generated; do not edit) |

## Commands

```bash
# Build/test (repo root)
nuke Compile
nuke Test

# Backend
dotnet run --project src/qyl.collector

# Frontend
npm run dev --prefix src/qyl.dashboard

# Docker (repo root)
docker compose -f eng/compose.yaml up -d
```

## Security

- Default posture: local/dev friendly; production hardening is explicit work
- Collector auth: optional token-based auth (see `QYL_TOKEN` / `src/qyl.collector/Auth/`)
- No direct DB access outside collector; all consumers use HTTP APIs
- Future: add authz/authn only when needed (JWT/API key), keep collector the enforcement point
