# qyl. Schema Unification Migration

Status: archived (pre-4-project migration). Current source of truth is `CLAUDE.md`.

## Problem

```
BEFORE: 5+ places define Span schema
├── src/qyl.collector/Models/ParsedSpan.cs
├── src/qyl.collector/Storage/DuckDbStore.cs (SpanRecord)
├── src/qyl.collector/Storage/DuckDbSchema.cs
├── src/qyl.grpc/Models/SpanModel.cs
├── src/qyl.grpc/Models/*.cs (6 model files)
└── src/qyl.collector/Contracts/Contracts.cs

AFTER: TypeSpec → Kiota → Single model
├── core/specs/otel/span.tsp (SOURCE OF TRUTH)
├── core/generated/dotnet/Models/Span.cs
└── All projects reference generated models
```

## Phase 1: Delete Duplicate Models

```bash
# These files duplicate what Kiota generates
rm src/qyl.collector/Models/ParsedSpan.cs
rm src/qyl.collector/Contracts/Contracts.cs
rm src/qyl.grpc/Models/SpanModel.cs
rm src/qyl.grpc/Models/TraceModel.cs
rm src/qyl.grpc/Models/LogModel.cs
rm src/qyl.grpc/Models/MetricModel.cs
rm src/qyl.grpc/Models/ResourceModel.cs
rm src/qyl.grpc/Streaming/StreamingContracts.cs
```

**Keep:**
- `src/qyl.grpc/Models/AttributeValue.cs` - Used by gRPC services
- `src/qyl.collector/Storage/DuckDbStore.cs` - Storage implementation (but update SpanRecord)

## Phase 2: Move Misplaced Code

```
MOVE: src/qyl.grpc/Protocol/OtlpHttpHandler.cs
  TO: src/qyl.collector/Protocol/OtlpHttpHandler.cs

MOVE: src/qyl.grpc/Api/SessionEndpoints.cs
  TO: src/qyl.collector/Api/SessionEndpoints.cs

MOVE: src/qyl.grpc/Streaming/SseExtensions.cs
  TO: src/qyl.collector/Realtime/SseExtensions.cs
```

**Reason:** HTTP handlers don't belong in gRPC project.

## Phase 3: Unify DuckDB Schema

**Authoritative schema:** `src/qyl.collector/Storage/DuckDbSchema.cs`

```csharp
// DELETE inline schema from DuckDbStore.cs (lines 35-100)
// USE DuckDbSchema.CreateSchemaAsync() instead
```

**Schema columns (v2.0):**
| Column | Type | Notes |
|--------|------|-------|
| trace_id | VARCHAR(32) | Hex-encoded |
| span_id | VARCHAR(16) | Hex-encoded |
| start_time_unix_nano | BIGINT | NOT TIMESTAMPTZ |
| end_time_unix_nano | BIGINT | |
| kind | UTINYINT | 0-5 enum |
| status_code | UTINYINT | 0-2 enum |
| gen_ai.* | Quoted columns | OTel 1.38 |
| attributes | MAP(VARCHAR,VARCHAR) | NOT JSON |

## Phase 4: Fix DuckDB.NET 1.4.x API

```csharp
// ❌ OLD (throws in 1.4.x)
cmd.Parameters.Add(new DuckDBParameter("name", value));

// ✅ NEW
cmd.Parameters.Add(new DuckDBParameter { ParameterName = "name", Value = value });
// OR
cmd.Parameters.AddWithValue("name", value);
```

## Phase 5: Reference Generated Models

Add to csproj files:

```xml
<ItemGroup>
  <Compile Include="$(MSBuildThisFileDirectory)../../core/generated/dotnet/**/*.cs"
           LinkBase="Generated" />
</ItemGroup>
```

Or create package:
```xml
<PackageReference Include="qyl.Client" Version="1.0.0" />
```

## Phase 6: Verify Build

```bash
# Clean build
rm -rf src/*/bin src/*/obj
dotnet restore
dotnet build

# Run tests
nuke Test

# Verify frontend types
npm run typecheck --prefix src/qyl.dashboard
```

## File Mapping

| Before | After |
|--------|-------|
| `collector/Models/ParsedSpan.cs` | DELETE (use generated) |
| `collector/Contracts/Contracts.cs` | DELETE (use generated) |
| `grpc/Models/SpanModel.cs` | DELETE (use generated) |
| `grpc/Models/TraceModel.cs` | DELETE |
| `grpc/Models/LogModel.cs` | DELETE |
| `grpc/Models/MetricModel.cs` | DELETE |
| `grpc/Models/ResourceModel.cs` | DELETE |
| `grpc/Streaming/StreamingContracts.cs` | DELETE |
| `grpc/Protocol/OtlpHttpHandler.cs` | MOVE → `collector/Protocol/` |
| `grpc/Api/SessionEndpoints.cs` | MOVE → `collector/Api/` |
| `collector/Storage/DuckDbStore.cs` schema | DELETE inline, use DuckDbSchema |
| `collector/Query/SessionAggregator.cs` | DELETE (use SessionQueryService) |

## Migration Checklist

- [ ] Delete duplicate model files (Phase 1)
- [ ] Move HTTP handlers from grpc to collector (Phase 2)
- [ ] Remove inline schema from DuckDbStore (Phase 3)
- [ ] Update DuckDB parameter API (Phase 4)
- [ ] Add generated model references (Phase 5)
- [ ] Verify clean build (Phase 6)
- [ ] Update imports in all files
- [ ] Run full test suite
- [ ] Update CHANGELOG.md

## Rollback

If migration fails:
```bash
git checkout -- src/qyl.collector/Models/
git checkout -- src/qyl.grpc/Models/
git checkout -- src/qyl.collector/Storage/DuckDbStore.cs
```

## Notes

- DuckDbSchema.cs is the single source of truth for storage
- TypeSpec/Kiota models are the single source for API types
- Don't mix: storage schema ≠ API schema (different purposes)
- SpanRecord (storage DTO) maps TO/FROM generated Span (API DTO)
