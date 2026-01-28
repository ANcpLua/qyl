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
tagline: "question your logs -> don't need to anymore"
domain: GenAI telemetry (OTel 1.39 semconv)
```

## architecture

```yaml
components:
  collector:
    path: src/qyl.collector
    sdk: ANcpLua.NET.Sdk.Web
    ports: [5100 (http), 4317 (grpc)]
    role: primary-runtime (this IS qyl from user perspective)

  dashboard:
    path: src/qyl.dashboard
    runtime: node-22
    framework: react-19 + vite-7
    embedding: collector/wwwroot/

  protocol:
    path: src/qyl.protocol
    sdk: ANcpLua.NET.Sdk
    constraint: bcl-only (zero packages)

  mcp:
    path: src/qyl.mcp
    sdk: ANcpLua.NET.Sdk
    protocol: model-context-protocol (stdio)

  servicedefaults:
    path: src/qyl.servicedefaults
    sdk: ANcpLua.NET.Sdk
    role: aspire-style defaults

  generators:
    path: src/qyl.instrumentation.generators
    sdk: ANcpLua.NET.Sdk
    role: roslyn-source-generators
```

## dependencies

```yaml
allowed:
  - collector -> protocol (ProjectReference)
  - mcp -> protocol (ProjectReference)
  - dashboard -> collector (HTTP at runtime)
  - mcp -> collector (HTTP at runtime)

forbidden:
  - mcp -> collector (ProjectReference) # must use HTTP
  - protocol -> any-package # must stay BCL-only
  - dashboard -> any-dotnet # pure frontend
```

## tech-stack

```yaml
dotnet:
  runtime: .NET 10.0 LTS
  lang: C# 14
  sdk: ANcpLua.NET.Sdk (1.7.2)

frontend:
  runtime: Node 22
  framework: React 19
  build: Vite 7
  styling: Tailwind CSS 4

testing:
  framework: xUnit v3 (3.2.2)
  runner: Microsoft.Testing.Platform v2

otel:
  semconv: "1.39.0"
  sdk: "1.15.0"
```

## commands

```yaml
development:
  collector: dotnet run --project src/qyl.collector
  dashboard: cd src/qyl.dashboard && npm run dev

build:
  full: nuke Full
  generate: nuke Generate --force-generate
  docker: nuke DockerBuild

test:
  all: dotnet test
```

## documentation-map

```yaml
root: CLAUDE.md (this file)
rules: .claude/rules/*.md
core: core/CLAUDE.md (TypeSpec schema)
eng: eng/CLAUDE.md (NUKE build)
collector: src/qyl.collector/CLAUDE.md
dashboard: src/qyl.dashboard/CLAUDE.md
protocol: src/qyl.protocol/CLAUDE.md
mcp: src/qyl.mcp/CLAUDE.md
generators: src/qyl.instrumentation.generators/CLAUDE.md
servicedefaults: src/qyl.servicedefaults/CLAUDE.md
tests: tests/CLAUDE.md
```
