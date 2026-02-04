---
name: ancplua-ecosystem
description: |
  Master context for the ANcpLua ecosystem. Use when working across multiple repos (ErrorOrX, Analyzers, SDK, qyl, ServiceDefaults, Template) to understand relationships, shared patterns, and source-of-truth locations. Invoke before any cross-repo refactoring or cohesion work.
---

## Source Metadata

```yaml
# none
```


# ANcpLua Ecosystem Context

## Repository Hierarchy

```
ANcpLua.Roslyn.Utilities  ← SOURCE OF TRUTH for Roslyn helpers
        ↓
    ┌───┴───┬─────────────┬─────────────────┐
    ↓       ↓             ↓                 ↓
ANcpLua  ANcpLua       ErrorOrX         qyl
.NET.Sdk .Analyzers   (generator)   (observability)
    ↓       ↓             ↓                 ↓
    └───┬───┴─────────────┴─────────────────┘
        ↓
  ┌─────┴─────┐
  ↓           ↓
ServiceDefaults  Template
(instrumentation) (clean arch)
```

## Source of Truth Locations

| Concern | Source of Truth | Do NOT Duplicate In |
|---------|-----------------|---------------------|
| Roslyn utilities | `ANcpLua.Roslyn.Utilities` | Any repo |
| MSBuild SDK patterns | `ANcpLua.NET.Sdk` | Consumer projects |
| Analyzer patterns | `ANcpLua.Analyzers` | Other generators |
| ErrorOr endpoint patterns | `ErrorOrX` | Template, consumers |
| OTel instrumentation | `ServiceDefaults1` | qyl, consumers |
| TypeSpec schemas | `qyl/core/specs/` | Generated code |

## Key Shared Utilities (from Roslyn.Utilities)

Before writing ANY helper code in dependent repos, check if these exist:

| Utility | Location | Purpose |
|---------|----------|---------|
| `EquatableArray<T>` | `EquatableArray.cs` | Value-equality arrays for generator caching |
| `DiagnosticFlow<T>` | `DiagnosticFlow.cs` | Railway-oriented error accumulation |
| `SymbolMatch.*` | `Matching/` | Fluent symbol pattern matching DSL |
| `Guard.NotNull()` | `Guard.cs` | Argument validation with CallerArgumentExpression |
| `*.OrEmpty()` | `EnumerableExtensions.cs` | Null-safe collection operations |
| `*.GetOrNull()` | `TryExtensions.cs` | Dictionary/collection safe access |
| `TypeSymbolExtensions` | `TypeSymbolExtensions.cs` | Type hierarchy analysis |

## Repository Summaries

### ANcpLua.Roslyn.Utilities
**Path**: `/Users/ancplua/ANcpLua.Roslyn.Utilities`
**Purpose**: Shared Roslyn helper library (17,200+ lines)
**Key exports**: EquatableArray, DiagnosticFlow, SymbolMatch, Guard, Extensions
**Consumers**: ErrorOrX.Generators, ANcpLua.Analyzers, qyl generators

### ANcpLua.NET.Sdk
**Path**: `/Users/ancplua/ANcpLua.NET.Sdk`
**Purpose**: Opinionated MSBuild SDK with auto-injection
**Key features**: CPM enforcement, analyzer injection, test framework setup, polyfills
**Consumers**: All other repos via `global.json`

### ANcpLua.Analyzers
**Path**: `/Users/ancplua/ANcpLua.Analyzers`
**Purpose**: 44 Roslyn diagnostics (AL0001-AL0044) with code fixes
**Key patterns**: ALAnalyzer base class, OperationHelper, WellKnownTypes
**Auto-injected by**: ANcpLua.NET.Sdk

### ErrorOrX
**Path**: `/Users/ancplua/ErrorOrX`
**Purpose**: Source generator converting ErrorOr<T> → ASP.NET endpoints
**Key patterns**: Minimal interface principle, AOT wrapper pattern, smart binding
**Dependencies**: ANcpLua.Roslyn.Utilities for pipeline helpers

### qyl
**Path**: `/Users/ancplua/qyl`
**Purpose**: AI observability platform (OTLP collector + GenAI analytics)
**Key patterns**: TypeSpec-first, DuckDB storage, MCP server, SSE streaming
**Protocol**: BCL-only types in qyl.protocol

### ServiceDefaults1
**Path**: `/Users/ancplua/ServiceDefaults1`
**Purpose**: Zero-config OTel instrumentation for GenAI + DB
**Key patterns**: Interceptors, runtime decorators, OTel SemConv v1.39
**Entry point**: `builder.UseQyl()` → `app.MapQylEndpoints()`

### Template
**Path**: `/Users/ancplua/Template/Template/src`
**Purpose**: Clean Architecture ASP.NET Core template
**Key patterns**: EndpointGroupBase, MediatR CQRS, layered architecture
**Based on**: Jason Taylor's CleanArchitecture template

## Cohesion Principles

1. **Single Source of Truth** — Never duplicate logic that exists upstream
2. **Minimal Interface** — Use smallest API surface (e.g., ErrorOr: IsError/Errors/Value only)
3. **Breaking Changes OK** — Public API stability is secondary to code quality
4. **Compile-Time > Runtime** — Prefer source generators over reflection
5. **Null = Empty** — Collections treat null as empty throughout
6. **Fully Qualified Names** — Generated code uses `global::` to avoid conflicts

## Cross-Repo Refactoring Checklist

When optimizing code across repos:

1. **Check Roslyn.Utilities first** — Does a utility already exist?
2. **Consider upstreaming** — Should this helper live in Roslyn.Utilities?
3. **Update consumers** — If changing Roslyn.Utilities, bump version in all repos
4. **Verify build chain** — Changes cascade: Utilities → SDK → Analyzers → ErrorOrX

## Executable Scripts

### Check All Repos Build

```bash
#!/bin/bash
# check-all-builds.sh - Verify entire ecosystem builds
set -e
REPOS=(
  "/Users/ancplua/ANcpLua.Roslyn.Utilities"
  "/Users/ancplua/ANcpLua.NET.Sdk"
  "/Users/ancplua/ANcpLua.Analyzers"
  "/Users/ancplua/ErrorOrX"
  "/Users/ancplua/qyl"
  "/Users/ancplua/ServiceDefaults1"
)

for repo in "${REPOS[@]}"; do
  echo "=== Building $(basename "$repo") ==="
  cd "$repo"
  dotnet build *.slnx --no-restore -v q || { echo "FAILED: $repo"; exit 1; }
done
echo "=== All repos build successfully ==="
```

### Run All Tests

```bash
#!/bin/bash
# run-all-tests.sh - Full test suite across ecosystem
set -e
REPOS=(
  "/Users/ancplua/ANcpLua.Roslyn.Utilities"
  "/Users/ancplua/ANcpLua.Analyzers"
  "/Users/ancplua/ErrorOrX"
  "/Users/ancplua/qyl"
  "/Users/ancplua/ServiceDefaults1"
)

for repo in "${REPOS[@]}"; do
  echo "=== Testing $(basename "$repo") ==="
  cd "$repo"
  dotnet test --solution *.slnx --no-build -v q || { echo "FAILED: $repo"; exit 1; }
done
echo "=== All tests pass ==="
```

### Check NuGet Versions

```bash
#!/bin/bash
# check-versions.sh - Compare local vs NuGet versions
PACKAGES=("ancplua.net.sdk" "ancplua.analyzers" "ancplua.roslyn.utilities")

for pkg in "${PACKAGES[@]}"; do
  echo "=== $pkg ==="
  curl -s "https://api.nuget.org/v3-flatcontainer/$pkg/index.json" | jq -r '.versions[-1]'
done
```

## Common Build Commands

```bash
# Any repo
dotnet build *.slnx
dotnet test --solution *.slnx

# After Roslyn.Utilities changes
cd /Users/ancplua/ANcpLua.Roslyn.Utilities && dotnet pack -c Release
# Then update Directory.Packages.props in consumers
```

## Version Coordination

All repos use Central Package Management. Key versions to keep aligned:

| Package | Purpose | Update Together |
|---------|---------|-----------------|
| `ANcpLua.Roslyn.Utilities` | Roslyn helpers | All generators |
| `Microsoft.CodeAnalysis.CSharp` | Roslyn APIs | All analyzers/generators |
| `xunit.v3` + `xunit.v3.mtp-v2` | Testing | All test projects |
