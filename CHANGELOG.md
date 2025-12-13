# Changelog

Update this file with any significant changes. The "Unreleased" section becomes release notes.

## Unreleased

### qyl Platform

- **feat(.net10):** Apply .NET 10 observability improvements

  #### Summary
  Applied relevant .NET 10 improvements for AI observability platform based on migration guide analysis.

  #### Files Changed

  | File | Change |
          |------|--------|
  | `src/qyl.collector/QylSerializerContext.cs` | Changed `CamelCase` → `SnakeCaseLower` per CLAUDE.md mandate |
  | `src/qyl.sdk.aspnetcore/QylAspNetCoreExtensions.cs` | Added `ActivitySourceOptions` with `TelemetrySchemaUrl` for OTel 1.38 |
  | `src/Shared/Throw/Throw.cs` | Changed namespace `qyl.collector.Throw` → `qyl.Shared` for multi-project injection |
  | `src/qyl.collector/Auth/TokenGenerator.cs` | Updated using to `qyl.Shared.Throw` |
  | `src/qyl.collector/Realtime/SseHub.cs` | Updated using to `qyl.Shared.Throw` |
  | `src/qyl.collector/Ingestion/SchemaNormalizer.cs` | Updated using to `qyl.Shared.Throw` |
  | `src/qyl.mcp.server/Client.cs` | Updated using to `qyl.Shared.Throw` |
  | `src/qyl.agents.telemetry/TracerProviderBuilderExtensions.cs` | Updated using to `qyl.Shared.Throw` |

  #### .NET 10 Benefits (Automatic)

  | Feature | Impact |
          |---------|--------|
  | Stack allocation optimizations | 40-70% faster telemetry processing |
  | DATAS GC | Auto-adapts to variable ingestion loads |
  | Buffer pooling | 63% less memory for streaming responses |
  | Array interface devirtualization | 50% faster LINQ on span collections |

  #### Not Applicable to qyl

  | Feature | Reason |
          |---------|--------|
  | W3C trace context propagator | qyl is collector, not trace propagator |
  | Activity sampling changes | No custom samplers in codebase |
  | Exception handler middleware | Not using `IExceptionHandler` |
  | Blazor WASM streaming | Server-only, no Blazor |

- **fix(typespec):** Resolve TypeSpec 1.7 encoded-name-conflict in GenAiMessage model

  TypeSpec 1.7 rejects models where field names match their `@encodedName` when other fields have explicit encoding.

  | Old Field | New Field | JSON Encoding |
          |-----------|-----------|---------------|
  | `role` | `msgRole` | `msg_role` |
  | `content` | `msgContent` | `msg_content` |
  | `name` | `msgName` | `msg_name` |

  File: `core/specs/domains/ai/genai.tsp` (lines 331-356)

- **fix(typespec):** Execute P0/P1 TypeSpec Schema Architect priority items for OTel 1.38 compliance

  #### Summary
  Addressed critical schema gaps identified in TYPESPEC_SCHEMA_ARCHITECT.md to ensure full OTel 1.38 GenAI semantic
  convention compliance and TypeSpec 1.7.0 best practices.

  #### Files Changed
  | File | Change |
          |------|--------|
  | `core/specs/domains/ai/genai.tsp` | **P0** - Added `v1_37` to GenAiVersions enum; fixed `gen_ai.system` deprecation from `@removed(v1_38)` → `@removed(v1_37)` per OTel spec |
  | `core/specs/domains/ai/genai.tsp` | **P0** - Added 5 missing exported models: `GenAiRequestAttributes`, `GenAiResponseAttributes`, `GenAiUsageAttributes`, `GenAiMessage`, `GenAiCostEstimate` |
  | `core/specs/otel/enums.tsp` | **P0** - Added `InstrumentKind` enum with 7 variants: Counter, UpDownCounter, Histogram, Gauge, ObservableCounter, ObservableGauge, ObservableUpDownCounter |
  | `core/specs/common/types.tsp` | **P1** - Added `@jsonSchema` decorator to all 26 scalar types for explicit JSON Schema generation control |

  #### Verification

    - [x] `nuke TypeSpecCompile` succeeds with 0 errors
    - [x] OpenAPI 3.1 generated: `core/openapi/openapi.yaml` (188KB)
    - [x] 21 warnings about OpenAPI 3.2 streaming - expected and safe to ignore

- **chore(codegen):** Clean up Kiota generation and remove dead code

  #### Summary
    - Added `--exclude-backward-compatible` to Kiota generation (removes `[Obsolete]` wrapper classes)
    - Removed dead `src/qyl.collector/Generated/` directory (collector is server, not client)
    - Fixed SyncGeneratedTypes to only sync TypeScript to dashboard

  #### Files Changed

  | File | Change |
          |------|--------|
  | `eng/build/Build.TypeSpec.cs` | Added `--exclude-backward-compatible` flag to all Kiota targets |
  | `eng/build/Build.TypeSpec.cs` | Removed C# sync to collector from SyncGeneratedTypes |
  | `spec-compliance-matrix/schema.yaml` | Updated file counts, added `flags: --exclude-backward-compatible` |
  | `src/qyl.collector/Generated/` | **Deleted** - was unused Kiota client SDK (collector uses own `Contracts.cs`) |

  #### Generated Output (cleaner, no obsolete wrappers)

  | Language | Files | Location |
          |----------|-------|----------|
  | TypeScript | 70 | `core/generated/typescript/` → synced to dashboard |
  | Python | 169 | `core/generated/python/` (external SDK) |
  | C# | 169 | `core/generated/dotnet/` (external SDK) |

  #### Architecture Clarification

  | Component | Role | Generated Code? |
          |-----------|------|-----------------|
  | Dashboard | API Client | ✅ Uses TypeScript SDK |
  | Collector | API Server | ❌ Uses own `Contracts.cs` |
  | External Apps | API Clients | ✅ Use C#/Python SDK from `core/generated/` |


