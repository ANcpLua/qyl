# Agent Task: Fix OTLP Timestamp Type Mismatch (long vs ulong)

**Task ID:** TIMESTAMP-TYPE-003
**Complexity:** Medium-High
**Estimated Tokens:** ~100k for full analysis + fix
**Success Criteria:** All timestamp types are `ulong`, DuckDB uses `UBIGINT`, build passes

---

## Your Mission

You are fixing a **type mismatch** where OpenTelemetry timestamps should be **unsigned 64-bit integers** (`ulong`) but qyl uses **signed 64-bit integers** (`long`) in several places. This violates the OTel specification and risks data precision loss.

---

## Context You Need

### The OTel Specification

OpenTelemetry Protocol defines timestamps as `fixed64` (protobuf) = `ulong` (C#):

```protobuf
// From opentelemetry-proto/trace.proto
message Span {
  fixed64 start_time_unix_nano = 7;  // UNSIGNED 64-bit
  fixed64 end_time_unix_nano = 8;    // UNSIGNED 64-bit
}
```

**Why unsigned?** Timestamps are nanoseconds since Unix epoch (1970). Negative values are meaningless.

### Repository Structure
```
/Users/ancplua/qyl/
├── src/qyl.collector/
│   ├── Ingestion/
│   │   └── OtlpTypes.cs          # Wire format types (HAS long - WRONG)
│   └── Storage/
│       └── DuckDbSchema.cs       # Database DDL (check column types)
├── src/qyl.protocol/
│   ├── Models/
│   │   ├── SpanRecord.cs         # Uses UnixNano (correct wrapper)
│   │   └── SessionSummary.cs     # Has DurationNs (check type)
│   └── Primitives/
│       └── UnixNano.cs           # Wrapper (uses ulong internally - CORRECT)
```

### The Problem Visualized
```
OTLP Wire Format          OtlpTypes.cs           SpanRecord.cs        DuckDB
────────────────          ────────────           ─────────────        ──────
fixed64 (unsigned)   →    long (WRONG!)     →    UnixNano (ulong) →   BIGINT (signed!)
         │                      │                                           │
         │                      ↓                                           ↓
         │              Truncation risk                              Overflow risk
         │              if value > long.Max                          for large values
         │
         └─── OTel Spec says: ulong ───────────────────────────────────────┘
```

### Numeric Ranges
| Type | Min | Max | Notes |
|------|-----|-----|-------|
| `ulong` | 0 | 18.4 quintillion | OTel spec |
| `long` | -9.2 quintillion | 9.2 quintillion | Current impl (WRONG) |
| Gap | N/A | ~9.2 quintillion | Values we can't store! |

**Practical impact:** `long` overflows in year 2262. But invalid (negative) values can occur NOW if JSON deserializes large numbers incorrectly.

---

## Files to Read First

Read these files IN ORDER:

1. `/Users/ancplua/qyl/src/qyl.protocol/Primitives/UnixNano.cs` - See the correct wrapper
2. `/Users/ancplua/qyl/src/qyl.collector/Ingestion/OtlpTypes.cs` - Find the `long` types to fix
3. `/Users/ancplua/qyl/src/qyl.protocol/Models/SpanRecord.cs` - Check DurationNs property
4. `/Users/ancplua/qyl/src/qyl.protocol/Models/SessionSummary.cs` - Check DurationNs property
5. `/Users/ancplua/qyl/src/qyl.collector/Storage/DuckDbSchema.cs` - Check SQL column types

---

## Step-by-Step Fix

### Step 1: Fix OtlpTypes.cs

```csharp
// BEFORE
public sealed class OtlpSpan
{
    [JsonPropertyName("startTimeUnixNano")]
    public long StartTimeUnixNano { get; init; }  // WRONG

    [JsonPropertyName("endTimeUnixNano")]
    public long EndTimeUnixNano { get; init; }    // WRONG
}

// AFTER
public sealed class OtlpSpan
{
    [JsonPropertyName("startTimeUnixNano")]
    public ulong StartTimeUnixNano { get; init; }  // CORRECT

    [JsonPropertyName("endTimeUnixNano")]
    public ulong EndTimeUnixNano { get; init; }    // CORRECT
}
```

**Important:** Search for ALL `long` types in OtlpTypes.cs that represent timestamps or durations.

### Step 2: Fix DuckDB Schema

```sql
-- BEFORE (check DuckDbSchema.cs for actual DDL)
CREATE TABLE spans (
    start_time BIGINT,    -- signed, max 9.2e18
    end_time BIGINT       -- signed, max 9.2e18
);

-- AFTER
CREATE TABLE spans (
    start_time UBIGINT,   -- unsigned, max 18.4e18
    end_time UBIGINT      -- unsigned, max 18.4e18
);
```

Find and update the DDL string in `/Users/ancplua/qyl/src/qyl.collector/Storage/DuckDbSchema.cs`.

### Step 3: Fix DurationNs Properties

In SpanRecord.cs and SessionSummary.cs:

```csharp
// BEFORE - may have issues
public long DurationNs => EndTime.Value - StartTime.Value;

// AFTER - with validation
public long DurationNs
{
    get
    {
        if (EndTime.Value < StartTime.Value)
        {
            throw new InvalidOperationException("EndTime cannot be before StartTime");
        }
        var duration = EndTime.Value - StartTime.Value;
        // Duration in nanoseconds - long is sufficient for ~292 years
        return (long)duration;
    }
}
```

**Note:** Duration can stay as `long` because no reasonable span duration exceeds 292 years. But add validation!

### Step 4: Update Any Conversion Code

Search for places that convert between `long` and `ulong`:

```bash
grep -r "StartTimeUnixNano\|EndTimeUnixNano" /Users/ancplua/qyl/src --include="*.cs"
```

Ensure conversions are explicit and safe:

```csharp
// When converting from OtlpSpan to SpanRecord
var record = new SpanRecord
{
    StartTime = new UnixNano(otlpSpan.StartTimeUnixNano),  // ulong → UnixNano
    EndTime = new UnixNano(otlpSpan.EndTimeUnixNano),
};
```

### Step 5: Database Migration (If Needed)

If there's existing data:

```sql
-- Option A: Alter column type (DuckDB supports this)
ALTER TABLE spans ALTER COLUMN start_time TYPE UBIGINT;
ALTER TABLE spans ALTER COLUMN end_time TYPE UBIGINT;

-- Option B: Recreate table
CREATE TABLE spans_new AS SELECT * FROM spans;
DROP TABLE spans;
-- Then recreate with UBIGINT and INSERT back
```

Check if migration is needed by looking at storage code.

---

## Validation Checklist

Before declaring success:

- [ ] `OtlpTypes.cs` uses `ulong` for all timestamp fields
- [ ] `DuckDbSchema.cs` uses `UBIGINT` for timestamp columns
- [ ] `SpanRecord.cs` DurationNs has validation (EndTime >= StartTime)
- [ ] `SessionSummary.cs` DurationNs has validation
- [ ] No implicit `ulong` to `long` conversions without validation
- [ ] `dotnet build` passes
- [ ] Search for remaining `long` timestamp usage: `grep -r "TimeUnixNano.*long" src/`

---

## Test Cases to Consider

```csharp
// Test 1: Normal timestamp
var normalTime = 1736294400000000000UL;  // ~2025
Assert.True(normalTime < (ulong)long.MaxValue);  // Fits in long

// Test 2: Large timestamp (year 2262+)
var largeTime = (ulong)long.MaxValue + 1000;
// Should work with ulong, would fail with long

// Test 3: Invalid duration (end before start)
var start = new UnixNano(1000);
var end = new UnixNano(500);
Assert.Throws<InvalidOperationException>(() =>
    new SpanRecord { StartTime = start, EndTime = end }.DurationNs);
```

---

## If You Get Stuck

### Problem: Can't change OtlpTypes because of existing JSON parsing
**Solution:** System.Text.Json handles `ulong` fine. Just change the type.

### Problem: DuckDB migration is complex
**Solution:** If this is dev environment, just recreate the database. Document migration for prod.

### Problem: Many files to change
**Solution:** Focus on the core path: OtlpTypes → SpanRecord → DuckDB. Other usages can be follow-up.

### Problem: Build errors after changing types
**Solution:** Look for implicit conversions. Add explicit casts where needed:
```csharp
// If you need long from ulong
long duration = (long)ulongValue;  // Add range check first!
```

---

## DuckDB Type Reference

| C# Type | DuckDB Type | Notes |
|---------|-------------|-------|
| `ulong` | `UBIGINT` | Unsigned 64-bit |
| `long` | `BIGINT` | Signed 64-bit |
| `uint` | `UINTEGER` | Unsigned 32-bit |
| `int` | `INTEGER` | Signed 32-bit |

---

## Output Format

When complete, provide:

1. **Summary:** What types were changed from long to ulong
2. **Files Changed:** List with specific line changes
3. **Migration Notes:** Any database changes needed
4. **Validation Added:** What runtime checks were added
5. **Build Result:** Output of `dotnet build`
