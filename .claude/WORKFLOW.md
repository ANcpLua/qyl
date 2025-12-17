# Active Workflow State

> Auto-synced by workflow-sync hook. **READ THIS FIRST** at session start.
> Last updated: 2025-12-18T01:30:00Z

## Current Task: Code Generation Pipeline Fix

**Goal:** Fix NUKE code generation so all generated types compile correctly.

**Phase:** P0 (Foundation) - VS-01 Span Ingestion PARTIAL, VS-02 List Sessions IN_PROGRESS

**Status:** 🟢 BUILD + UNIT TESTS PASSING - Generator work complete

## Git Status

```
<!-- Auto-updated by hook on session end -->
```

## Build Status

**PASSING** ✅ - All projects compile successfully

Fixed in this session:
1. ✅ `GenAiSemconvExtensions.g.cs` - Removed invalid SearchValues API, use StartsWith
2. ✅ `TraceId/SpanId` - Added TryFormat, IsEmpty, Parse(byte[]), IFormatProvider overload
3. ✅ `UnixNano` - Added Zero, ToTimeSpan, ToDateTimeOffset
4. ✅ `OtlpAttributes.cs` - Fixed Duration calculation
5. ✅ `OtlpJsonSpanParser.cs` - Fixed long->ulong, string->Guid conversions
6. ✅ `Tests` - Fixed SchemaVersion->OtlpSchemaVersion, ulong literals, Guid session ID
7. ✅ `CSharpGenerator` - Use NumberStyles.HexNumber for hex types (TraceId, SpanId)

**Test Results:**
- Unit tests: 115/115 PASS ✅
- Integration tests: 25 FAIL (web host startup issue - separate from generator work)

## Current Issues

### Issue 1: Generated Primitives Missing APIs - RESOLVED ✅
**Severity:** BLOCKER
**Location:** `src/qyl.protocol/Primitives/*.g.cs`

The CSharpGenerator produces primitives (TraceId, SpanId, UnixNano, SessionId) that are missing methods the existing code expects:

| Missing Method | Type | Used In |
|----------------|------|---------|
| `TryFormat(Span<char>)` | TraceId, SpanId | TraceServiceImpl, Parser |
| `IsEmpty` | TraceId, SpanId | OtlpJsonSpanParser |
| `Zero` | UnixNano | OtlpJsonSpanParser |
| `ToTimeSpan()` | UnixNano (via ulong) | OtlpAttributes |
| `Parse(ReadOnlySpan<byte>)` | TraceId, SpanId | Grpc, Parser |

**Root Cause:** Hand-written primitives had rich API surface; generator produces minimal types.

**Recommended Fix:** Update CSharpGenerator to emit complete primitive types with all methods.

### Issue 2: OTelSemconvGenerator Invalid Code - RESOLVED ✅
**Severity:** HIGH
**Location:** `eng/build/Domain/CodeGen/OTelSemconvGenerator.cs`

Removed invalid `SearchValues<string>.ContainsAny()`. Replaced with simple `StartsWith` checks.

### Issue 3: NUKE Refactor Complete
**Severity:** RESOLVED ✅
**Details:**
- Build.ClaudeMd.cs deleted
- IGenerate has GenerateOTelSemconv + MigrateOTelSemconv
- Build.TypeSpec has GenerateKiota* targets
- IOtlpSmoke added
- Build.cs implements all interfaces

## Completed Steps (This Workflow)

1. ✅ NUKE refactor - all targets renamed, IOtlpSmoke added
2. ✅ Deleted Build.ClaudeMd.cs (redundant)
3. ✅ Fixed DuckDbGenerator namespace/quote escaping
4. ✅ Fixed QylSerializerContext partial modifier
5. ✅ Added GenAiAttributes.Extra.cs with missing constants
6. ✅ Telemetry documentation saved to docs/architecture/
7. ✅ eng/CLAUDE.md updated with full documentation
8. ✅ Workflow sync hook created

## Next Steps

1. ✅ ~~Fix CSharpGenerator~~ - DONE
2. ✅ ~~Fix OTelSemconvGenerator~~ - DONE
3. ✅ ~~Regenerate~~ - DONE
4. ✅ ~~Build & verify~~ - DONE

**Remaining:**
5. **Run tests** - `dotnet test` to verify functionality
6. **Complete VS-01** - Mark Span Ingestion as COMPLETE
7. **Update ADRs** - Document generator changes
8. **Commit** - Create commit with all generator fixes

## Files Modified This Workflow

| File | Change |
|------|--------|
| `eng/build/Build.cs` | Added IOtlpSmoke interface |
| `eng/build/Components/IOtlpSmoke.cs` | NEW - gzip smoke test |
| `eng/build/Components/IGenerate.cs` | Added MigrateOTelSemconv |
| `eng/build/Build.TypeSpec.cs` | Renamed to GenerateKiota* |
| `eng/build/Domain/CodeGen/README.md` | NEW - two-system docs |
| `.claude/hooks/workflow-sync.sh` | NEW - preserves state |
| `docs/architecture/dotnet-telemetry-evolution.md` | NEW - .NET 10 telemetry |

## Decision Log

| Decision | Rationale |
|----------|-----------|
| Keep generated primitives | Single source of truth in QylSchema.cs |
| Fix generators, not consuming code | Generators should produce complete types |
| Add workflow sync hook | Prevent context drift across sessions |
| Two code gen systems | QylSchema=internal, TypeSpec=external API |

---

**Session Instructions:**
1. Read this file at session start to restore context
2. Continue from "Next Steps" section
3. Update "Completed Steps" as you work
4. Update this file before ending session
5. Run hook manually: `.claude/hooks/workflow-sync.sh`
