# qyl — ACTUAL Project Structure

> For Claude Code review. Shows discrepancies between docs and reality.

## CRITICAL: docs/ files are WRONG

The following files describe a **DIFFERENT** structure than what exists:
- `docs/qyl-architecture.yaml` — describes `core/emitters/`, `shared/Throw/`, `sdk/` that DON'T EXIST
- `docs/COMPLETE_STRUCTURE.txt` — fantasy structure, not reality

## ACTUAL Structure (December 2024)

```
qyl/
├── CLAUDE.md                          # Root AI context (CORRECT)
├── qyl.slnx                           # Solution file (CORRECT)
├── Directory.Build.props              # SDK layering entry
├── Directory.Build.targets
├── Directory.Packages.props           # Central Package Management
│
├── core/                              # EXISTS - TypeSpec + Generated
│   ├── CLAUDE.md
│   ├── specs/                         # TypeSpec source files
│   │   ├── main.tsp                   # Entry point
│   │   ├── api/                       # REST API definitions
│   │   │   ├── routes.tsp
│   │   │   └── streaming.tsp
│   │   ├── otel/                      # OTel primitives
│   │   │   ├── span.tsp
│   │   │   ├── logs.tsp
│   │   │   ├── metrics.tsp
│   │   │   └── resource.tsp
│   │   ├── common/                    # Shared types
│   │   │   ├── errors.tsp
│   │   │   ├── pagination.tsp
│   │   │   └── types.tsp
│   │   └── domains/                   # Domain models (40+ files)
│   │       ├── ai/
│   │       ├── observe/
│   │       ├── ops/
│   │       └── ...
│   │
│   └── generated/                     # Kiota output (DO NOT EDIT)
│       └── dotnet/
│           ├── QylClient.cs
│           ├── Models/                # 100+ generated models
│           └── V1/                    # Request builders
│
├── src/
│   │
│   ├── qyl.protocol/                  # LEAF - Shared contracts
│   │   ├── CLAUDE.md
│   │   ├── qyl.protocol.csproj
│   │   ├── Primitives/
│   │   │   ├── SessionId.cs           # ⚠️ DUPLICATED in collector!
│   │   │   └── UnixNano.cs
│   │   ├── Models/
│   │   │   ├── SpanRecord.cs
│   │   │   ├── GenAiSpanData.cs
│   │   │   ├── SessionSummary.cs
│   │   │   └── TraceNode.cs
│   │   ├── Attributes/
│   │   │   └── GenAiAttributes.cs
│   │   └── Contracts/
│   │       ├── ISpanStore.cs
│   │       └── ISessionAggregator.cs
│   │
│   ├── qyl.collector/                 # Backend
│   │   ├── CLAUDE.md
│   │   ├── qyl.collector.csproj
│   │   ├── Program.cs
│   │   │
│   │   ├── Primitives/                # ⚠️ DUPLICATION - should use qyl.protocol
│   │   │   ├── SessionId.cs
│   │   │   ├── SpanId.cs
│   │   │   ├── TraceId.cs
│   │   │   └── UnixNano.cs
│   │   │
│   │   ├── GenAiAttributes.cs         # ⚠️ DUPLICATION - should use qyl.protocol
│   │   ├── GenAiExtractor.cs
│   │   ├── QylAttributes.cs
│   │   ├── TracerProviderBuilderExtensions.cs
│   │   │
│   │   ├── Auth/
│   │   │   ├── TokenAuth.cs
│   │   │   └── TokenGenerator.cs
│   │   ├── ConsoleBridge/
│   │   │   └── ConsoleBridge.cs
│   │   ├── Contracts/
│   │   │   └── Contracts.cs           # ⚠️ Should use qyl.protocol
│   │   ├── Ingestion/
│   │   │   ├── OtlpAttributes.cs
│   │   │   ├── OtlpJsonSpanParser.cs
│   │   │   ├── OtlpTypes.cs
│   │   │   └── SchemaNormalizer.cs
│   │   ├── Mapping/
│   │   │   └── Mappers.cs
│   │   ├── Mcp/
│   │   │   └── McpServer.cs
│   │   ├── Models/
│   │   │   └── ParsedSpan.cs          # ⚠️ Should use generated
│   │   ├── Query/
│   │   │   └── SessionQueryService.cs
│   │   ├── Realtime/
│   │   │   ├── SseEndpoints.cs
│   │   │   ├── SseHub.cs
│   │   │   └── TelemetrySseStream.cs
│   │   └── Storage/
│   │       ├── DuckDbSchema.cs        # SINGLE SOURCE for DDL
│   │       └── DuckDbStore.cs
│   │
│   ├── qyl.mcp/                       # MCP Server
│   │   ├── CLAUDE.md
│   │   ├── qyl.mcp.csproj
│   │   ├── Program.cs
│   │   ├── Client.cs
│   │   └── Tools/
│   │       ├── TelemetryJsonContext.cs
│   │       └── TelemetryTools.cs
│   │
│   ├── qyl.dashboard/                 # React Frontend
│   │   ├── CLAUDE.md
│   │   └── (React/Vite project)
│   │
│   ├── Shared/                        # Injected code
│   │   └── Throw/
│   │       └── Throw.cs
│   │
│   └── LegacySupport/                 # Polyfills
│       ├── CallerAttributes/
│       ├── DiagnosticAttributes/
│       └── IsExternalInit/
│
├── eng/
│   ├── MSBuild/
│   │   ├── Shared.props
│   │   ├── Shared.targets
│   │   └── LegacySupport.targets
│   │
│   ├── build/
│   │   ├── build.csproj               # NUKE build
│   │   ├── Build.cs
│   │   ├── Build.Frontend.cs
│   │   ├── Build.TypeSpec.cs
│   │   │
│   │   ├── Components/                # ACTIVE components
│   │   │   ├── IHasSolution.cs
│   │   │   ├── ICompile.cs
│   │   │   ├── ITest.cs
│   │   │   └── ...
│   │   │
│   │   ├── codex/                     # ⭐ FUTURE: Schema-driven generation
│   │   │   ├── QylSchema.cs           # SINGLE SOURCE OF TRUTH candidate!
│   │   │   ├── IEmitter.cs
│   │   │   ├── CSharpEmitter.cs
│   │   │   ├── DuckDbEmitter.cs
│   │   │   ├── TypeScriptEmitter.cs
│   │   │   ├── IHasSolution.Theory.cs
│   │   │   └── Build.cs
│   │   │
│   │   └── theory/                    # ⭐ FUTURE: Polyrepo structure
│   │       ├── IHasSolution.cs        # Different paths for polyrepo
│   │       ├── ITypeSpec.cs
│   │       ├── IGenerationGuard.cs
│   │       └── IAnalyzerConfig.cs
│   │
│   ├── qyl.analyzers/                 # Roslyn analyzers
│   │   └── ...
│   │
│   └── qyl.cli/                       # CLI tool
│       └── ...
│
├── examples/
│   ├── qyl.AspNetCore.Example/
│   └── qyl.analyzers.Sample/
│
├── tests/
│   └── UnitTests/
│       ├── qyl.analyzers.tests/
│       └── qyl.mcp.server.tests/
│
└── docs/                              # ⚠️ MOSTLY WRONG
    ├── qyl-architecture.yaml          # ❌ Describes non-existent structure
    ├── COMPLETE_STRUCTURE.txt         # ❌ Fantasy, not reality
    ├── ACTUAL_STRUCTURE.md            # ✅ THIS FILE
    └── (other legacy docs)
```

## Key Issues Found

### 1. DUPLICATION between qyl.protocol and qyl.collector

```
qyl.protocol/Primitives/SessionId.cs    ←→  qyl.collector/Primitives/SessionId.cs
qyl.protocol/Primitives/UnixNano.cs     ←→  qyl.collector/Primitives/UnixNano.cs
qyl.protocol/Attributes/GenAiAttributes.cs  ←→  qyl.collector/GenAiAttributes.cs
```

**Fix**: Collector should reference qyl.protocol, not duplicate.

### 2. codex/QylSchema.cs is the RIGHT approach

The `eng/build/codex/QylSchema.cs` defines all models in ONE place:
- Primitives (SessionId, UnixNano, TraceId, SpanId)
- Models (SpanRecord, GenAiSpanData, SessionSummary, TraceNode)
- DuckDB table definitions
- OTel gen_ai.* attributes

This should drive generation into:
- `qyl.protocol/` (C# models)
- `qyl.collector/Storage/DuckDbSchema.cs` (DDL)
- `qyl.dashboard/src/types/` (TypeScript)

### 3. theory/ vs Components/ confusion

```
eng/build/Components/IHasSolution.cs   # ACTIVE - current monorepo paths
eng/build/theory/IHasSolution.cs       # FUTURE - polyrepo paths (core/, collector/, sdk/)
```

### 4. docs/ describes wrong structure

`qyl-architecture.yaml` mentions:
- `core/emitters/duckdb/` — DOESN'T EXIST (emitter is in eng/build/codex/)
- `shared/Throw/` — WRONG PATH (it's src/Shared/Throw/)
- `sdk/` folder — DOESN'T EXIST

## Recommended Actions

1. **Delete or update** `docs/qyl-architecture.yaml` and `docs/COMPLETE_STRUCTURE.txt`
2. **Remove duplicates** from qyl.collector, use qyl.protocol
3. **Integrate codex/** — generate from QylSchema.cs
4. **Choose** theory/ OR Components/ — not both
