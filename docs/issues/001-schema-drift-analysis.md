# Agent Task: Fix Schema Drift Between QylSchema.cs and Runtime Models

**Task ID:** SCHEMA-DRIFT-001
**Complexity:** High
**Estimated Tokens:** ~150k for full analysis + fix
**Success Criteria:** `dotnet build` passes with zero schema-related errors

---

## Your Mission

You are fixing a **schema drift problem** where code generation produces types incompatible with runtime models. There are currently 22+ build errors caused by this drift.

---

## Context You Need

### Repository Structure
```
/Users/ancplua/qyl/
├── eng/build/Domain/CodeGen/
│   ├── QylSchema.cs           # Schema definitions (SOURCE OF TRUTH per CLAUDE.md)
│   ├── Generators/            # Code generators
│   └── *.g.cs                 # Generated files (STALE - cause errors)
├── src/qyl.protocol/
│   ├── Models/
│   │   ├── SpanRecord.cs      # Runtime model (DIVERGED from schema)
│   │   └── SessionSummary.cs  # Runtime model (DIVERGED from schema)
│   └── Primitives/
│       └── UnixNano.cs        # Timestamp wrapper (ulong internally)
└── src/qyl.collector/
    └── Storage/
        └── DuckDbSchema.cs    # Database DDL
```

### The Problem Visualized
```
QylSchema.cs (defines)          SpanRecord.cs (actual)
────────────────────            ──────────────────────
ulong StartTimeUnixNano    →    UnixNano StartTime        MISMATCH!
ulong EndTimeUnixNano      →    UnixNano EndTime          MISMATCH!
15 properties              →    22 properties              MISSING!
string-based IDs           →    Same                       OK

Generated DuckDbSchema.g.cs tries to use QylSchema types on SpanRecord → FAILS
```

### Build Errors You're Fixing
```
CS0266: Cannot implicitly convert type 'ulong' to 'long'
CS0117: 'SpanRecord' does not contain a definition for 'StartTimeUnixNano'
```

---

## Files to Read First

Read these files IN ORDER to understand the current state:

1. `/Users/ancplua/qyl/CLAUDE.md` - Lines about "Type Ownership" and "Single Source of Truth"
2. `/Users/ancplua/qyl/eng/build/Domain/CodeGen/QylSchema.cs` - Current schema definition
3. `/Users/ancplua/qyl/src/qyl.protocol/Models/SpanRecord.cs` - Current runtime model
4. `/Users/ancplua/qyl/src/qyl.protocol/Models/SessionSummary.cs` - Current runtime model
5. `/Users/ancplua/qyl/src/qyl.protocol/Primitives/UnixNano.cs` - Timestamp wrapper

---

## Decision Point

After reading, choose ONE approach:

### Option A: Delete Stale Generated Files (Quick Fix)
```bash
rm /Users/ancplua/qyl/eng/build/Domain/CodeGen/*.g.cs
```
- Pros: Immediate build fix
- Cons: Doesn't fix root cause, generators broken

### Option B: Update QylSchema.cs to Match SpanRecord.cs (Proper Fix)
- Update QylSchema.cs properties to match SpanRecord.cs exactly
- Ensure generators produce compatible code
- Pros: Fixes root cause
- Cons: More work, need to understand generators

### Option C: Delete QylSchema.cs, Use SpanRecord.cs Directly (Architectural Change)
- Remove code generation for protocol types
- Use SpanRecord.cs as source of truth
- Pros: No drift possible
- Cons: Lose multi-target generation (TypeScript, SQL)

---

## Recommended Approach: Option B

### Step 1: Audit Current Differences

Create a comparison table by reading both files:

```markdown
| Property | QylSchema.cs | SpanRecord.cs | Action |
|----------|--------------|---------------|--------|
| TraceId | string | string | OK |
| StartTime | ulong StartTimeUnixNano | UnixNano StartTime | UPDATE SCHEMA |
| ... | ... | ... | ... |
```

### Step 2: Update QylSchema.cs

The schema should produce types compatible with SpanRecord. Example fix:

```csharp
// BEFORE in QylSchema.cs
public ulong StartTimeUnixNano { get; init; }

// AFTER - matches SpanRecord.cs pattern
[CSharpType("UnixNano")]
[JsonPropertyName("startTimeUnixNano")]
public ulong StartTimeUnixNano { get; init; }
```

### Step 3: Update Generators

Check if generators in `/Users/ancplua/qyl/eng/build/Domain/CodeGen/Generators/` handle:
- UnixNano type mapping
- Property name transformations
- All 22 properties from SpanRecord

### Step 4: Regenerate and Test

```bash
# Delete old generated files
rm /Users/ancplua/qyl/eng/build/Domain/CodeGen/*.g.cs

# Regenerate (if generator works)
dotnet run --project /Users/ancplua/qyl/eng/build -- Generate

# Or just build (will use manual models)
dotnet build /Users/ancplua/qyl/qyl.slnx
```

---

## Validation Checklist

Before declaring success:

- [ ] `dotnet build /Users/ancplua/qyl/qyl.slnx` passes
- [ ] No CS0266 (type conversion) errors
- [ ] No CS0117 (missing property) errors
- [ ] SpanRecord.cs and QylSchema.cs are consistent
- [ ] Document what you changed

---

## If You Get Stuck

### Problem: Generators are complex
**Solution:** Just delete the .g.cs files for now. Document that generators need fixing.

### Problem: UnixNano type not in schema
**Solution:** Add `[CSharpType("UnixNano")]` attribute support or use raw ulong with conversion.

### Problem: Too many differences
**Solution:** Focus on the properties that cause build errors first. List others as TODOs.

---

## Output Format

When complete, provide:

1. **Summary:** What you fixed
2. **Files Changed:** List with brief description
3. **Remaining Issues:** What still needs work
4. **Build Result:** Output of `dotnet build`
