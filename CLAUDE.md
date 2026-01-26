# qyl

AI Observability Platform. Observe everything. Judge nothing. Document perfectly.

## cross-cutting-rules

See @.claude/rules/architecture-rules.md for type ownership and dependencies.
See @.claude/rules/coding-patterns.md for .NET 10 patterns and banned APIs.
See @.claude/rules/genai-semconv.md for OTel 1.39 GenAI semantic conventions.
See @.claude/rules/build-workflow.md for NUKE targets and Docker build.
See @.claude/rules/codegen.md for generated code rules (never edit *.g.cs).
See @.claude/rules/frontend.md for React 19 and TypeScript guidelines.

## identity

```yaml
name: qyl
type: observability-platform
tagline: "question your logs → don't need to anymore"
domain: gen_ai telemetry (OTel 1.39 semconv)
```

## distribution

```yaml
primary:
    method: docker
    image: ghcr.io/ancplua/qyl
    command: |
        docker run -d \
          -p 5100:5100 \
          -p 4317:4317 \
          -v ~/.qyl:/data \
          ghcr.io/ancplua/qyl:latest

secondary:
    method: dotnet-global-tool
    command: |
        dotnet tool install -g qyl
        qyl start
```

## architecture

```yaml
components:
    collector:
        runtime: dotnet-10
        sdk: ANcpLua.NET.Sdk.Web
        ports:
            http: 5100
            grpc: 4317
        responsibilities:
            - otlp-ingestion (grpc + http)
            - rest-api (/api/v1/*)
            - sse-streaming (/api/v1/live)
            - static-files (embedded dashboard)
            - duckdb-storage

    dashboard:
        runtime: node-22
        framework: react-19
        build: vite-7
        output: dist/
        embedding: collector/wwwroot/

    protocol:
        runtime: dotnet-10
        sdk: ANcpLua.NET.Sdk
        constraint: bcl-only (zero packages)
        role: leaf-dependency

    mcp:
        runtime: dotnet-10
        sdk: ANcpLua.NET.Sdk
        protocol: model-context-protocol (stdio)
        connection: http-only to collector
```

## tech-stack

> **Note**: Package versions listed below are for reference only and may become outdated.
> For current versions, check Version.props, Directory.Packages.props, and package.json.

```yaml
dotnet:
    runtime: .NET 10.0 (LTS)
    lang: C# 14
    sdk: ANcpLua.NET.Sdk (see Directory.Packages.props)
    sdk-variants:
        - ANcpLua.NET.Sdk        # libraries, console
        - ANcpLua.NET.Sdk.Web    # ASP.NET Core
        - ANcpLua.NET.Sdk.Test   # xUnit v3

packages:
    storage: DuckDB.NET.Data.Full
    grpc: Grpc.AspNetCore
    protobuf: Google.Protobuf

frontend:
    runtime: Node 22
    framework: React 19
    build: Vite 7
    styling: Tailwind CSS 4
    state: TanStack Query 5
    components: Radix UI
    charts: Recharts
    icons: Lucide React

testing:
    framework: xunit.v3 (3.2.2)
    assertions: AwesomeAssertions (9.3.0)
    runner: Microsoft.Testing.Platform v2

otel:
    semconv: "1.39.0"
    sdk: "1.15.0"
    attributes:
        - gen_ai.system
        - gen_ai.request.model
        - gen_ai.response.model
        - gen_ai.usage.input_tokens
        - gen_ai.usage.output_tokens
        - gen_ai.request.temperature
        - gen_ai.response.finish_reasons
        - gen_ai.tool.name
        - gen_ai.tool.call_id
```

## schema-generation

```yaml
source-of-truth: core/specs/main.tsp

typespec:
    version: "1.8.0"
    packages:
        - "@typespec/compiler"
        - "@typespec/http"
        - "@typespec/rest"
        - "@typespec/openapi"
        - "@typespec/openapi3"
        - "@typespec/json-schema"
        - "@typespec/versioning"
        - "@typespec/sse"
        - "@typespec/events"

generator: SchemaGenerator.cs
location: eng/build/Domain/CodeGen/SchemaGenerator.cs

outputs:
    - target: core/openapi/openapi.yaml
      from: typespec

    - target: src/qyl.protocol/Primitives/Scalars.g.cs
      generator: GenerateScalars()
      content: strongly-typed wrappers (TraceId, SpanId, SessionId, etc.)
      features:
          - IParsable<T>
          - ISpanFormattable
          - ReadOnlySpan<byte> hot-path parsing
          - JsonConverter per type

    - target: src/qyl.protocol/Enums/Enums.g.cs
      generator: GenerateEnums()
      content: OTel enums (SpanKind, StatusCode, SeverityNumber)
      features:
          - JsonNumberEnumConverter for integer enums
          - JsonStringEnumConverter for string enums
          - EnumMember attributes

    - target: src/qyl.protocol/Models/*.g.cs
      generator: GenerateModels()
      content: record types with JSON serialization

    - target: src/qyl.collector/Storage/DuckDbSchema.g.cs
      generator: GenerateDuckDb()
      content: DDL statements with indexes

    - target: src/qyl.dashboard/src/types/api.ts
      generator: openapi-typescript (npm)

extensions-read:
    - x-csharp-type      # C# type name override
    - x-duckdb-table     # marks model as DuckDB table
    - x-duckdb-column    # column name override  
    - x-duckdb-type      # DuckDB type override
    - x-duckdb-primary-key
    - x-duckdb-index     # creates index
    - x-primitive        # marks as strongly-typed wrapper
    - x-promoted         # promoted from attributes_json
    - x-enum-varnames    # enum member names
```

## dependencies

```yaml
allowed:
    - from: dashboard
      to: collector
      via: http (rest/sse at runtime)

    - from: mcp
      to: collector
      via: http (rest at runtime)

    - from: collector
      to: protocol
      via: ProjectReference

    - from: mcp
      to: protocol
      via: ProjectReference

forbidden:
    - from: mcp
      to: collector
      via: ProjectReference
      reason: must communicate via http for decoupling

    - from: dashboard
      to: any-dotnet
      via: any
      reason: pure frontend build artifact

    - from: protocol
      to: any-external-package
      via: PackageReference
      reason: must remain bcl-only leaf
```

## build

```yaml
system: nuke
entry: eng/build/Build.cs

targets:
    TypeSpecCompile:
        input: core/specs/*.tsp
        output: core/openapi/openapi.yaml
        command: tsp compile main.tsp

    Generate:
        depends: [TypeSpecCompile]
        generator: SchemaGenerator.Generate()
        guard: GenerationGuard
        options:
            --force-generate: overwrite existing
            --dry-run: preview only
        ci-behavior: fails if stale files detected

    DashboardBuild:
        working-dir: src/qyl.dashboard
        command: npm run build
        output: dist/

    Compile:
        command: dotnet build

    DashboardEmbed:
        depends: [DashboardBuild, Compile]
        action: copy dist/ → collector/wwwroot/
        critical: true

    Publish:
        depends: [DashboardEmbed]
        command: dotnet publish -c Release

    DockerBuild:
        depends: [Publish]
        dockerfile: Dockerfile
        tag: ghcr.io/ancplua/qyl:latest

    Pack:
        depends: [Publish]
        output: *.nupkg (global tool)
```

## conventions

```yaml
files:
    generated: "*.g.cs"
    never-edit: "*.g.cs", "api.ts", "openapi.yaml"

namespaces:
    primitives: Qyl.Common
    enums: Qyl.Enums
    models: Qyl.Models
    storage: qyl.collector.Storage

tables:
    spans: spans
    sessions: sessions
    logs: logs

api-paths:
    rest: /api/v1/*
    otlp-http: /v1/traces
    sse: /api/v1/live

ports:
    http: 5100
    grpc: 4317
```

## commands

```yaml
development:
    collector: dotnet run --project src/qyl.collector
    dashboard: cd src/qyl.dashboard && npm run dev
    full-stack: nuke Dev

build:
    full: nuke Full
    generate: nuke Generate --force-generate
    docker: nuke DockerBuild
    pack: nuke Pack

test:
    all: dotnet test
    coverage: nuke Coverage
```

## specialized-agents

For complex tasks, use domain-specific agents via the Task tool:

```yaml
qyl-collector:
  focus: OTLP ingestion, DuckDB, REST API, SSE
  skills: [/docs-lookup, /review, systematic-debugging]
  definition: .claude/agents/qyl-collector.md

qyl-build:
  focus: NUKE, TypeSpec, codegen, Docker
  skills: [/slice-validate, /type-ownership]
  definition: .claude/agents/qyl-build.md

qyl-dashboard:
  focus: React 19, real-time UI, TanStack
  skills: [/frontend-design, brainstorming]
  definition: .claude/agents/qyl-dashboard.md

deep-think-partner:
  focus: Complex reasoning, architecture decisions
  model: opus (extended thinking)
  definition: .claude/agents/deep-think-partner.md
```

When working on a component, invoke its specialist agent for focused expertise.
