# eng - Build System

NUKE build system and MSBuild infrastructure.

## Directory Structure

```
build/                  # NUKE build project
  Build.cs              # Main entry, target orchestration
  BuildCore.cs          # Core targets (Compile, Test)
  BuildInfra.cs         # Infrastructure (Docker, Pack)
  BuildPipeline.cs      # CI/CD pipeline
  BuildTest.cs          # Test targets
  BuildVerify.cs        # Verification targets
  SchemaGenerator.cs    # TypeSpec -> C#/DuckDB/TS codegen

MSBuild/                # Shared MSBuild infrastructure
  Shared.props          # Common properties
  Shared.targets        # Common targets
```

## NUKE Targets

### Build Pipeline

```
TypeSpecCompile -> Generate -> Compile -> Test
                                  |
FrontendBuild -----> DockerImageBuild (embeds dashboard in multi-stage build)
```

### Target Reference

| Target             | Purpose                 | Command                 |
|--------------------|-------------------------|-------------------------|
| `TypeSpecCompile`  | TypeSpec -> OpenAPI     | `nuke TypeSpecCompile`  |
| `Generate`         | OpenAPI -> C#/DuckDB/TS | `nuke Generate`         |
| `FrontendBuild`    | Build React app         | `nuke FrontendBuild`    |
| `Compile`          | Build .NET projects     | `nuke Compile`          |
| `DockerImageBuild` | Build Docker images     | `nuke DockerImageBuild` |
| `Test`             | Run tests               | `nuke Test`             |
| `Coverage`         | Run with coverage       | `nuke Coverage`         |
| `Full`             | Complete build          | `nuke Full`             |

## Common Commands

```bash
# Full build (TypeSpec -> Docker)
nuke Full

# Regenerate all types
nuke Generate --force-generate

# CI - fails if generated files are stale
nuke Generate

# Build and test
nuke Compile Test

# Docker image only
nuke DockerBuild

# Coverage report
nuke Coverage
```

## SchemaGenerator

`SchemaGenerator.cs` reads `openapi.yaml` and generates:

1. **C# Scalars** (`Primitives/Scalars.g.cs`)
    - Strongly-typed wrappers: TraceId, SpanId, SessionId
    - Implements IParsable, ISpanFormattable

2. **C# Enums** (`Enums/Enums.g.cs`)
    - SpanKind, StatusCode, SeverityNumber

3. **C# Models** (`Models/*.g.cs`)
    - Record types with JSON serialization

4. **DuckDB Schema** (`Storage/DuckDbSchema.g.cs`)
    - CREATE TABLE statements
    - Index definitions

5. **TypeScript Types** (`types/api.ts`)
    - Interface definitions for React

## Docker Build

```bash
# Build from repository root
docker build -f src/qyl.collector/Dockerfile -t qyl .
```

**Build Stages:**

1. `dashboard` - Node 22, builds React app
2. `build` - .NET SDK, restores and publishes
3. `runtime` - .NET ASP.NET runtime

**Important**: Uses Debian base (not Alpine) because DuckDB requires glibc.

## Docker Compose

```bash
# Start all services
docker compose -f eng/compose.yaml up -d

# Stop all services
docker compose -f eng/compose.yaml down
```

Services:

- `qyl-collector`: Backend API (5100, 4317)
- `qyl-dashboard`: React frontend (8080)
- `qyl-mcp`: MCP server (5200)

## Environment Variables

| Variable         | Default | Purpose             |
|------------------|---------|---------------------|
| `Configuration`  | Debug   | Build configuration |
| `FORCE_GENERATE` | false   | Force regeneration  |
