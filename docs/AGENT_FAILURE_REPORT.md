# Agent Failure Report — December 13, 2024

> For senior Claude review. Documents what a previous agent attempted and where it failed.

## Agent Session Summary

**Duration**: ~40 minutes
**Tasks Attempted**: 3
**Success Rate**: Partial (1 succeeded, 2 failed)

---

## Task 1: ANcpLua.NET.Sdk Restructuring

### Goal
Unify the SDK repo into a single package (remove Web/Test variants), with Meziantou-style folder layout.

### What Agent Did
1. Moved `common/` and `shared/` into `src/`
2. Removed Web/Test SDK variants (archived to `_obsolete/`)
3. Updated nuspec paths
4. Updated tests to use `IsTestProject` property instead of separate SDKs

### Failure Point
**MSBuild import paths broke after restructuring.**

```
error MSB4019: The imported project
"/Users/ancplua/.nuget/packages/ancplua.net.sdk/999.9.9/Sdk/Sdk.props" was not found.
```

The agent kept flip-flopping between:
- `Sdk/ANcpLua.NET.Sdk/Sdk.props` (correct for dev, wrong for package)
- `Sdk/Sdk.props` (correct for package)

**Root Cause**: The nuspec `target=` paths vs `_ANcpLuaSdkPackageRoot` calculation were inconsistent.

### Test Results After Agent
```
Failed: 317, Passed: 36, Skipped: 12, Total: 365
```

### What Needs Fixing
```xml
<!-- In nuspec, target must match what Sdk.props expects -->
<file src="Sdk/ANcpLua.NET.Sdk/Sdk.props" target="Sdk/Sdk.props" />

<!-- In Sdk.props, the path calculation must match -->
<_ANcpLuaSdkPackageRoot>$(MSBuildThisFileDirectory)..\</_ANcpLuaSdkPackageRoot>

<!-- This means: Sdk/Sdk.props + ..\ = Sdk/ → then common/ = Sdk/../common = common/ -->
<!-- BUT nuspec puts common at /common, not /Sdk/../common -->
```

**The actual fix**: Either:
1. Put `common/` and `configuration/` inside `Sdk/` in the nupkg, OR
2. Change `_ANcpLuaSdkPackageRoot` to `$(MSBuildThisFileDirectory)..\..\`

---

## Task 2: Kiota TypeScript Generation

### Goal
Set up `kiota generate` workflow for dashboard TypeScript types from OpenAPI.

### What Agent Found
- TypeSpec → OpenAPI pipeline already exists in `eng/build/theory/ITypeSpec.cs`
- Kiota command structure was documented
- Dashboard already has `src/types/generated/` with Kiota output

### Status: Already Working
The agent confirmed this is functional. The NUKE targets are:
```
nuke TypeSpecCompile      # TypeSpec → OpenAPI
nuke GenerateTypeScript   # Kiota → TypeScript
nuke SyncDashboardTypes   # Copy to dashboard
```

### No Failure Here
This was exploration, not implementation.

---

## Task 3: Custom DuckDB TypeSpec Emitter

### Goal
Build a custom TypeSpec emitter that generates:
- `schema.sql` (DDL)
- `DuckDbSchema.g.cs` (C# FrozenDictionary)

### What Agent Did
1. Researched TypeSpec emitter API
2. Read `@typespec/openapi3` implementation
3. Found existing `DuckDbSchema.cs` in collector

### Failure Point
**Agent ran out of time/context before implementing.**

The agent got stuck in analysis paralysis:
- Read package.json, openapi.js, main.tsp, decorators.tsp
- Searched for `$onEmit` patterns
- Read existing DuckDbSchema.cs
- Never actually wrote any emitter code

### What Was Needed (Not Done)

```
core/emitters/qyl.duckdb/
├── package.json           # TypeSpec emitter package
├── tsconfig.json
└── src/
    ├── index.ts           # Entry: $onEmit
    ├── emitter.ts         # Main emitter logic
    ├── sql-generator.ts   # Generate CREATE TABLE
    └── csharp-generator.ts # Generate FrozenDictionary
```

---

## Key Observations for Senior Review

### 1. Documentation vs Reality Gap

The agent discovered that `docs/qyl-architecture.yaml` and `docs/COMPLETE_STRUCTURE.txt` describe:
- `core/emitters/duckdb/` — DOESN'T EXIST
- `shared/Throw/` — WRONG (it's `src/Shared/Throw/`)
- `sdk/` folder — DOESN'T EXIST

### 2. Duplication in Codebase

```
qyl.protocol/Primitives/SessionId.cs  ←→  qyl.collector/Primitives/SessionId.cs
qyl.protocol/Attributes/GenAiAttributes.cs  ←→  qyl.collector/GenAiAttributes.cs
```

### 3. The `eng/build/codex/` Folder is the Right Approach

`QylSchema.cs` defines everything in ONE place:
- Primitives
- Models
- DuckDB tables
- OTel attributes

This should be the single source of truth for code generation.

### 4. theory/ vs Components/ Confusion

Two parallel implementations exist:
- `eng/build/Components/` — Current monorepo paths
- `eng/build/theory/` — Future polyrepo paths

No clear decision on which to use.

---

## Recommended Actions

### Immediate (< 1 hour)
1. Delete or update `docs/qyl-architecture.yaml` — it's wrong
2. Delete `docs/COMPLETE_STRUCTURE.txt` — it's fantasy

### Short-term (< 4 hours)
1. Fix ANcpLua.NET.Sdk nuspec/Sdk.props path alignment
2. Remove duplicates from qyl.collector (use qyl.protocol)

### Medium-term (< 1 day)
1. Implement codex emitters (C#, DuckDB, TypeScript)
2. Wire QylSchema.cs → code generation
3. Choose theory/ OR Components/ (not both)

---

## Agent Workflow Issues

The agent exhibited these patterns that led to failure:

1. **Analysis Paralysis**: Spent 6+ minutes reading TypeSpec internals without writing code
2. **Path Confusion**: Flip-flopped on nuspec target paths 3+ times
3. **No Verification Loop**: Made changes without testing intermediate states
4. **Context Exhaustion**: Ran out of useful context before completing tasks

### What Would Have Helped
- Clear decision upfront: "The nupkg structure will be X"
- Test after each nuspec change, not at the end
- Focus on one task at a time (not 3 parallel goals)
