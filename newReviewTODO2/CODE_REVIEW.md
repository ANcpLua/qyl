# Type Alignment Code Review - Issues & Fixes

## Executive Summary

The type alignment work is done, but code analysis revealed a **critical architectural gap**: the ingestion pipeline is
completely stubbed out. Sessions will always be empty because nothing processes incoming telemetry.

| Issue                                      | Severity    | Impact                                     |
|--------------------------------------------|-------------|--------------------------------------------|
| **Ingestion endpoints are stubs**          | 🔴 CRITICAL | Nothing gets stored, sessions always empty |
| **`TrackSpan` never called**               | 🔴 CRITICAL | Session aggregation doesn't work           |
| **`EnqueueAsync` never called**            | 🔴 CRITICAL | DuckDB always empty                        |
| Dead code calling non-existent endpoint    | 🔴 Critical | Runtime 404                                |
| Duplicate type definitions in serializer   | 🟡 Medium   | Confusion, maintenance burden              |
| GenAIPage uses incompatible legacy types   | 🟡 Medium   | Will break when mock → real data           |
| UTF-8 encoding bug in index.ts             | 🟢 Low      | Visual glitch                              |
| `decimal` → `double` precision loss        | 🟢 Low      | Minor currency rounding                    |
| Missing `await using`                      | 🟢 Low      | Resource cleanup in async methods          |
| Unused `_store` field in SessionAggregator | 🟢 Low      | Dead code or incomplete feature            |

---

## 🔴🔴 CRITICAL: Ingestion Pipeline Not Implemented

### The Problem

```csharp
// Program.cs lines 166-167 - These do NOTHING!
app.MapPost("/api/v1/ingest", () => Results.Accepted());
app.MapPost("/v1/traces", () => Results.Accepted());
```

The ingestion endpoints just return `Accepted` without:

- Parsing the request body
- Storing spans to DuckDB
- Calling `SessionAggregator.TrackSpan()`
- Broadcasting to SSE clients

**Result**: Sessions API will always return empty. DuckDB will always be empty. The dashboard shows nothing.

### The Fix

See `IngestionEndpoint.cs` for a complete working implementation that:

1. Parses incoming span batches
2. Calls `store.EnqueueAsync()` for persistence
3. Calls `aggregator.TrackSpan()` for each span
4. Broadcasts to SSE via `ITelemetrySseBroadcaster`

---

## 🔴 Critical Issues (Type Alignment)

### 1. Dead Code: `useResources()` calls non-existent endpoint

**File**: `src/qyl.dashboard/src/hooks/use-telemetry.ts`

```typescript
// Line 75-81 - This endpoint DOES NOT EXIST in Program.cs
export function useResources() {
    return useQuery({
        queryKey: telemetryKeys.resources(),
        queryFn: () => fetchJson<{ resources: Resource[] }>('/api/v1/resources'),  // ← 404!
        select: (data) => data.resources ?? [],
        refetchInterval: 5000,
    });
}
```

**Fix**: Either:

- A) Delete `useResources()` and the `Resource` import (it's not used)
- B) Add `/api/v1/resources` endpoint to backend

**Recommended**: Option A - delete dead code:

```typescript
// DELETE these lines from use-telemetry.ts:
import type {Resource} from '@/types/telemetry';  // DELETE

// DELETE the entire useResources function (lines 74-81)
```

---

## 🟡 Medium Issues

### 2. Duplicate/Dead Types in QylSerializerContext.cs

**File**: `src/qyl.collector/QylSerializerContext.cs`

**Problem**: Old response types still registered alongside new DTOs:

```csharp
// OLD (lines 37-39) - NO LONGER USED by any endpoint
[JsonSerializable(typeof(SessionsResponse))]
[JsonSerializable(typeof(SpansResponse))]
[JsonSerializable(typeof(TraceResponse))]

// NEW (lines 67-69) - Actually used
[JsonSerializable(typeof(SessionListResponseDto))]
[JsonSerializable(typeof(SpanListResponseDto))]
[JsonSerializable(typeof(TraceResponseDto))]

// And at bottom, old records still defined:
public sealed record SessionsResponse(IReadOnlyList<SessionSummary> Sessions, int Total, bool HasMore);
public sealed record SessionListResponse(IReadOnlyList<SessionSummary> Sessions, int Total, bool HasMore);
```

**Fix**: Replace with cleaned version (see /home/claude/fixes/QylSerializerContext.cs)

---

### 3. GenAIPage Uses Incompatible Types

**File**: `src/qyl.dashboard/src/pages/GenAIPage.tsx`

**Problem**: The agent reverted to legacy types because mock data has properties not in OpenAPI:

```typescript
// GenAIPage.tsx - uses OLD types
import type {Span, GenAISpanData} from '@/types/telemetry';

// Mock data uses properties that don't exist in API:
const mockGenAISpans = [{
    genai: {
        finishReasons: ['stop'],      // OpenAPI has 'finishReason' (singular)
        inputMessages: [...],         // NOT in OpenAPI
        outputMessages: [...],        // NOT in OpenAPI
        toolCalls: [...],             // NOT in OpenAPI
    }
}];
```

**Fix**: Two options:

**Option A (Quick)**: Keep mock data, but mark clearly that it's mock-only:

```typescript
// Types for mock data only - real API uses @/types
interface MockGenAISpanData extends GenAISpanData {
    finishReasons?: string[];  // Mock only
    inputMessages?: Message[];  // Mock only
    outputMessages?: Message[]; // Mock only
    toolCalls?: ToolCall[];     // Mock only
}
```

**Option B (Proper)**: Extend OpenAPI spec to include message content:

```yaml
# Add to openapi.yaml GenAISpanData:
inputMessages:
  type: array
  items:
    $ref: '#/components/schemas/GenAIMessage'
outputMessages:
  type: array
  items:
    $ref: '#/components/schemas/GenAIMessage'
toolCalls:
  type: array
  items:
    $ref: '#/components/schemas/ToolCall'
```

---

## 🟢 Low Priority Issues

### 4. UTF-8 Encoding Bug

**File**: `src/qyl.dashboard/src/types/index.ts`

```typescript
// Line 113 - corrupted μ character
if (ms < 1) return `${(ms * 1000).toFixed(0)}Î¼s`;  // Wrong
if (ms < 1) return `${(ms * 1000).toFixed(0)}μs`;   // Correct
```

**Fix**: See /home/claude/fixes/index.ts

---

### 5. Precision Loss: decimal → double

**File**: `src/qyl.collector/Mapping/Mappers.cs`

```csharp
// SessionAggregator.cs uses decimal:
public decimal TotalCostUsd { get; set; }

// Mapper casts to double (minor precision loss):
TotalCostUsd = (double)summary.TotalCostUsd,
```

**Impact**: `$0.000123456789` might become `$0.0001234567890000001`

**Fix**: Either:

- Keep as is (acceptable for display)
- Change OpenAPI spec and DTO to use string for currency (JSON has no decimal)
- Track cents as integers (multiply by 10000)

---

## Files Created

| File                                         | Purpose                              |
|----------------------------------------------|--------------------------------------|
| `/home/claude/fixes/QylSerializerContext.cs` | Cleaned serializer context           |
| `/home/claude/fixes/index.ts`                | Fixed UTF-8 encoding                 |
| `/home/claude/fixes/use-telemetry.ts`        | Removed dead `useResources()`        |
| `/home/claude/fixes/IngestionEndpoint.cs`    | **Working ingestion implementation** |
| `/home/claude/fixes/SessionAggregatorFix.cs` | Fixes for unused fields, await using |

---

## Additional Fixes Needed

### `await using` in DuckDbStore.cs

Change all `using var` to `await using var` in async methods for proper async disposal:

```csharp
// FROM:
using var cmd = _connection.CreateCommand();
using var reader = await cmd.ExecuteReaderAsync(ct);

// TO:
await using var cmd = _connection.CreateCommand();
await using var reader = await cmd.ExecuteReaderAsync(ct);
```

Lines affected: 195, 207, 257, 324, 333, 343, 392, 400, 414, 424, 465, 482

### Unused `_store` in SessionAggregator

Either:

- **Remove it** if you don't need persistence bootstrap
- **Use it** to restore sessions from DuckDB on startup (see `SessionAggregatorFix.cs`)

### Unused methods in DuckDbStore

These are likely future features not yet wired up:

- `GetSpansAsync` - Generic query (not used by current endpoints)
- `QueryParquetAsync` - Parquet cold tier (not implemented)
- `GetGenAiStatsAsync` - Stats endpoint (not implemented)

Either delete them or wire them to endpoints.

---

## Verification Commands

After applying fixes:

```bash
# Backend
dotnet build src/qyl.collector

# Frontend
npm run build --prefix src/qyl.dashboard

# Type check only
npx tsc --noEmit --prefix src/qyl.dashboard
```

---

## Summary of Changes Needed

### Priority 1: Critical (System doesn't work without these)

1. **IMPLEMENT** ingestion endpoint using `IngestionEndpoint.cs` template
2. **WIRE UP** `TrackSpan` to be called when spans arrive
3. **WIRE UP** `EnqueueAsync` to persist spans to DuckDB

### Priority 2: Type Cleanup (Already fixed in previous batch)

4. ✅ DELETE `useResources()` and `Resource` import from `use-telemetry.ts`
5. ✅ REPLACE `QylSerializerContext.cs` with cleaned version

### Priority 3: Code Quality

6. **FIX** `await using` in all async methods in DuckDbStore.cs
7. **DECIDE** on `_store` in SessionAggregator (remove or use for bootstrap)
8. **FIX** UTF-8 in `index.ts` (μ character)
9. **DECIDE** on GenAIPage approach (keep mock types separate OR extend OpenAPI)

The German proverb is exactly right - the ingestion pipeline gap would have caused days of debugging ("why are my
sessions empty?").
