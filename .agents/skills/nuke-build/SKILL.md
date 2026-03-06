---
name: nuke-build
description: Build and validate qyl using NUKE. Use after any code change, before PRs, or when builds break.
---

# NUKE Build

NUKE is the single build orchestrator. Never bypass it with raw dotnet commands.

## Targets

| Target | What it does | When to use |
|--------|-------------|-------------|
| `nuke` | Compile + Test (default) | After any code change |
| `nuke Ci` | Clean + Compile + Test + Coverage | Before PR |
| `nuke Full` | Ci + Generate + Verify + Frontend | Release readiness |
| `nuke UnitTests` | Unit tests only | Quick feedback loop |
| `nuke IntegrationTests` | Integration tests (needs Docker) | Before merge |
| `nuke Dev` | Docker + Compile, prints URLs | Starting a dev session |
| `nuke Clean` | Wipe bin/obj/Artifacts | When builds are broken |

## Decision Tree

```text
IF code changed           → nuke
IF schema changed         → nuke Generate --force-generate && nuke
IF before PR              → nuke Ci
IF release check          → nuke Full
IF quick unit feedback    → nuke UnitTests
IF integration tests      → nuke IntegrationTests
IF starting dev session   → nuke Dev
IF build is broken        → nuke Clean && nuke
```

## Wrong (never do this)

```bash
# Wrong: raw dotnet with hand-crafted flags
dotnet restore && dotnet build && dotnet test --no-build ...

# Wrong: bypassing analyzers
dotnet build -p:RunAnalyzers=false -p:TreatWarningsAsErrors=false

# Wrong: 20 chained commands
dotnet clean && dotnet restore && dotnet build --configuration Release ...
```

## Build Files

All in `eng/build/`:

| File | Responsibility |
|------|---------------|
| `Build.cs` | Entry point, orchestration targets (Ci, Full, Dev, Clean) |
| `BuildTest.cs` | MTP argument builder, IQylTest interface |
| `BuildCoverage.cs` | Coverage collection and reporting |
| `BuildPipeline.cs` | TypeSpec → OpenAPI → C#/DuckDB/TS codegen |
| `BuildVerify.cs` | Generated code validation, DuckDB DDL check |
| `BuildInfra.cs` | Docker image builds, Compose orchestration |
| `BuildPaths.cs` | Path definitions, versioning (IHazSourcePaths) |
