# qyl

[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/ANcpLua/qyl/badge)](https://scorecard.dev/viewer/?uri=github.com/ANcpLua/qyl)
[![OpenSSF Criticality](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FANcpLua%2Fqyl%2Fbadge%2Fcriticality.json)](docs/criticality.md)

A local OpenTelemetry investigation stack for .NET. Instrument an application with one
line, run the collector on your own machine, and read the traces, logs, and metrics back
through the embedded dashboard, the collector API, or MCP.

The latest public qyl release is 4.0.0, which is also the `main` branch source line. The
site and documentation are at [qyl.at](https://qyl.at/). The other two hosted surfaces
are endpoints rather than pages: `https://api.qyl.at` serves the collector read API and
OTLP ingest under their route prefixes, and `https://mcp.qyl.at/mcp` is the MCP endpoint,
which answers `401` to anything without an OAuth 2.1 bearer token.

## Run the stack

```bash
dotnet tool install --global qyl
qyl up
```

`qyl up` starts the collector and its embedded dashboard on `127.0.0.1:5100`, diagnostics
on `:5200`, OTLP ingestion on `:4318` and `:4317`, and the runner API on `:18889`.
Telemetry is stored under `~/.qyl/`, never in the working directory. All five ports are
checked up front, so a conflict fails the command instead of leaving a half-bound stack.

## Send telemetry from an application

```bash
dotnet add package Qyl.Telemetry.Hosting
```

```csharp
using Qyl;

builder.AddQyl();
```

`AddQyl()` is what wires the pipeline: it activates automatic instrumentation, registers
the qyl activity sources and meters, and exports traces, metrics, and logs over OTLP — to
`OTEL_EXPORTER_OTLP_ENDPOINT` when set, otherwise `QYL_ENDPOINT`, otherwise a collector
discovered on localhost. Environment variables on their own export nothing; without the
call there is no exporter to configure.

qyl stores and serves traces, logs, and metrics. Metric points land in a series index plus
a point table, and are queried by metric name, attribute matchers, a time range, and a step
— aggregated server-side into buckets, never returned as raw points. OTLP's summary point
is the one shape qyl declines: its pre-computed quantiles cannot be re-aggregated over a
window or merged across series, so it is reported back as a `partial_success` naming the
instrument rather than stored unqueryable.

## Artifacts and release lines

qyl is one dependency graph with several independently released packages. Each line
carries its own version — the 1.0.0 launch is an event, not a number every package
adopts. The versions below are the source and dependency lines `main` builds against, and
each is published. Package registries are authoritative for public availability.

| Package | `main` / release target | Repository |
| --- | --- | --- |
| `qyl` (dotnet tool) | 4.0.0 | this one |
| `Qyl.Telemetry.Hosting`, `Qyl.Telemetry.AutoInstrumentation*` | 13.0.0 | [Qyl.OpenTelemetry.AutoInstrumentation](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation) |
| `Qyl.Telemetry.SemanticConventions*` | 9.0.0 | [Qyl.OpenTelemetry.SemanticConventions](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions) |
| `Qyl.Api.Contracts`, `@ancplua/qyl-api-schema` | 10.0.0 | [qyl-api-schema](https://github.com/ANcpLua/qyl-api-schema) |
| `qyl-mcp-server` | 4.0.0 | [qyl.mcp](https://github.com/ANcpLua/qyl.mcp) |
| `Qyl.Api.Sdk` (MSBuild SDK) | 4.0.0 | this one |

`Qyl.Sdk` and the `Qyl.OpenTelemetry.*` package IDs are retired. They stop at their last
published versions and receive no further releases; the table above lists their
successors.

## Architecture

One wire, two generated loops, many independently shipped artifacts. The wire is OTLP: a
producer stack runs inside the customer's process and ends at an exporter, the collector is
a separate process that begins where that exporter ends, and no package crosses between
them. Loop 1 is the vocabulary — one Weaver registry generates the producer's constants and
the collector's ingest catalog, so qyl cannot emit telemetry its own collector does not
recognise. Loop 2 is the contract — one TypeSpec repository generates the collector's API
surface and every first-party client of it, so no client holds a shadow contract.

Every rule is owned by a compiler, an analyzer, a generator, or a gate. The gates live in
`eng/build` and run from the `Verify` and `Ci` targets; the package edge list they enforce
is the table in `eng/build/BuildDependencyEdges.cs`.

One graph, one truth, many artifacts.

## Samples

`samples/qyl.sample` is a Native AOT ASP.NET Core API built by `Qyl.Api.Sdk`, the MSBuild
SDK this repository publishes from `packages/Qyl.Api.Sdk`. It is the JetBrains Rider
*ASP.NET Core Web API (native AOT)* template with its `Todo` models kept; everything else it
has comes from the SDK, and every part of it is produced at compile time.

```csharp
builder.Services.AddQylApi(AppJsonSerializerContext.Default);
```

One call registers, in a fixed order: the given JSON contexts and the SDK's problem-details
context, validation, problem details, and the `v1` OpenAPI document. None of them is optional
in a Qyl API, so none is a `With*` step.

| Concern | Trigger in the sample | Produced by |
| --- | --- | --- |
| Validation | DataAnnotations on `CreateTodoRequest` | the `Microsoft.Extensions.Validation` generator, intercepting `AddValidation` |
| XML | `[GenerateXml]` and the `System.Xml.Serialization` attributes on `partial record Todo` | `Qyl.Sdk.Xml.Generator`, which emits `WriteXml` as plain `XmlWriter` calls and the same tree as `XmlShape` data |
| OpenAPI | `///` comments on handlers and contracts | the `Microsoft.AspNetCore.OpenApi` XML-comment generator, intercepting `AddOpenApi` |
| Committed contract | `dotnet build` | `Microsoft.Extensions.ApiDescription.Server`, writing `samples/qyl.sample/openapi/qyl.sample.json` |
| Binding and JSON | method-group handlers, `AppJsonSerializerContext` | the Request Delegate Generator and the `System.Text.Json` generator |

The sample reaches the SDK by importing `packages/Qyl.Api.Sdk/Sdk/Sdk.props` and `Sdk.targets`,
so a fresh clone builds without a pack step. A consumer outside this repository needs neither
import, no generator reference and no central package management:

```xml
<Project Sdk="Qyl.Api.Sdk/4.0.0">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
    </PropertyGroup>
</Project>
```

Overrides use the framework's own mechanisms after `AddQylApi` — `ValidationOptions`,
`OpenApiOptions` for the `v1` document — with one exception: the OpenAPI specification version
is the MSBuild property `QylOpenApiVersion`, because the build-time document tool takes it as
its own argument. Every SDK default is conditional on the property being empty, so a project
that sets it first wins. The reasons behind each of these choices are dated in
`packages/Qyl.Api.Sdk/DECISIONS.md`.

```bash
dotnet run --project samples/qyl.sample     # or docker compose -f eng/compose.yaml up qyl.sample
dotnet run --project eng/build/build.csproj -- ApiSdk
```

`ApiSdk` is the proof, in eight stages: build and generator tests, the committed contract
unchanged, every compile-time generator's output present, the HTTP scenario against the managed
host, a Native AOT publish carrying no managed files beside the binary, the same scenario
against the native executable, the container image, and a consumer built from the packed SDK
producing the identical contract. `Ci` runs it.

## Build and verify

Requires the .NET SDK pinned in `global.json`.

```bash
dotnet run --project eng/build/build.csproj -- Ci
```

`Ci` builds and tests the backend, builds and tests the dashboard, runs its Release-product
Playwright smoke, verifies the generated contract package, and checks the collector
semantic catalog.

## License

MIT
