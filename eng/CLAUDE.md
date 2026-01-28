# eng

NUKE build system and MSBuild infrastructure.

## structure

```yaml
build/              # NUKE build project
  Build.cs          # Main entry, target orchestration
  BuildCore.cs      # Core targets (Compile, Test)
  BuildInfra.cs     # Infrastructure (Docker, Pack)
  BuildPipeline.cs  # CI/CD pipeline
  BuildTest.cs      # Test targets
  BuildVerify.cs    # Verification targets
  SchemaGenerator.cs # TypeSpec -> C#/DuckDB/TS codegen

MSBuild/            # Shared MSBuild infrastructure
  Shared.props      # Common properties
  Shared.targets    # Common targets
  Shared.Claude.*   # CLAUDE.md generation system
```

## nuke-targets

```yaml
TypeSpecCompile:
  input: core/specs/*.tsp
  output: core/openapi/openapi.yaml

Generate:
  depends: [TypeSpecCompile]
  outputs:
    - src/qyl.protocol/*.g.cs
    - src/qyl.collector/Storage/DuckDbSchema.g.cs
    - src/qyl.dashboard/src/types/api.ts

DashboardBuild:
  command: npm run build
  output: src/qyl.dashboard/dist/

Compile:
  command: dotnet build

DashboardEmbed:
  depends: [DashboardBuild, Compile]
  action: copy dist/ -> collector/wwwroot/

Publish:
  depends: [DashboardEmbed]
  command: dotnet publish -c Release

DockerBuild:
  depends: [Publish]
  dockerfile: src/qyl.collector/Dockerfile
```

## commands

```yaml
full-build: nuke Full
generate: nuke Generate --force-generate
docker: nuke DockerBuild
test: nuke Test
coverage: nuke Coverage
```

## claude-md-generation

MSBuild-based system for auto-generating CLAUDE.md files from csproj metadata:

```xml
<PropertyGroup>
  <GenerateClaudeMd>true</GenerateClaudeMd>
  <ClaudePurpose>Description here</ClaudePurpose>
  <ClaudeRole>component-role</ClaudeRole>
</PropertyGroup>
```

Currently disabled in qyl (hand-written CLAUDE.md preferred).
