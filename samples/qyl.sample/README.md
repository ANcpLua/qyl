# qyl.sample — the Rider ASP.NET Core (Native AOT) template, extended by `qyl.sdk`

The project under `qyl.sample/` is the JetBrains Rider *ASP.NET Core Web API (native AOT)* template with its `Todo` models kept.
Everything it gained comes from `qyl.sdk/`: request validation, XML output, and a committed OpenAPI contract, each produced at
compile time. The SDK is the seed of `Qyl.Api.Sdk`, an MSBuild SDK for opinionated API design. This is the first alpha; it is kept small.

```csharp
using Qyl.Sample;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddQylApi(AppJsonSerializerContext.Default);
builder.Services.AddSingleton<TodoStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTodos();

app.Run();
```

One call. `AddQylApi` registers, in a fixed order: the given JSON contexts and the SDK's problem-details context, validation,
problem details, and the `v1` OpenAPI document. None of these is optional in a Qyl API, so none is a `With*` step; the returned
`IQylApiBuilder` is empty and reserved for genuine future choices (see [`qyl.sdk/DECISIONS.md`](qyl.sdk/DECISIONS.md)).

## What the SDK adds to the template

| Concern | Trigger in the sample | Produced by | Proof |
|---|---|---|---|
| Validation | DataAnnotations on `CreateTodoRequest` | `Microsoft.Extensions.Validation` generator (intercepts `AddValidation`) | `POST /todos/` with `"title":"no"` → `400 application/problem+json`, `errors.Title` |
| XML | `[GenerateXml]` + the `System.Xml.Serialization` attributes on the `partial record Todo` | `Qyl.Sdk.Xml.Generator` emits `IXmlWritable.WriteXml` as plain `XmlWriter` calls for the whole element tree (scalars, enums, nested `[GenerateXml]` models, collections, base classes) and the same tree as `XmlShape` data | `GET /todos/{id}/xml` → `200 application/xml`, `<todo id="6">…</todo>`; the contract describes it as `TodoXml` with `xml.name`, attributes, and formats; `Qyl.Sdk.Xml.Generator.Tests` proves the output equals `XmlSerializer`'s for the same attributes |
| OpenAPI | `///` comments on handlers and contracts | `Microsoft.AspNetCore.OpenApi` XML-comment generator (intercepts `AddOpenApi`) | Summaries, `<response>` texts, `application/problem+json` 400 and `application/xml` 200 in `/openapi/v1.json` |
| Committed contract | `dotnet build` | `Microsoft.Extensions.ApiDescription.Server` | `qyl.sample/openapi/qyl.sample.json`; the native binary serves the identical document |
| Binding and JSON | method-group handlers, `AppJsonSerializerContext` | Request Delegate Generator, `System.Text.Json` generator | `JsonSerializerIsReflectionEnabledByDefault=false`, `PublishAot=true` |

Every generator's output is written to `qyl.sample/obj/generated/` after a build; read it.

## Layout

```
qyl.sdk/
  Sdk/Sdk.props, Sdk/Sdk.targets      entry points; today imported explicitly, later <Project Sdk="Qyl.Api.Sdk">
  Build/Qyl.Sdk.Api.props             the opinions (AOT, RDG, reflection-free JSON, XML docs, contract directory, OpenAPI version)
  Build/Qyl.Sdk.Api.targets           implicit package references, generator reference, build-info generation, generated-file emission
  Build/Qyl.Sdk.Packages.props        the versions the SDK owns
  Sources/                            AddQylApi alone; compiled into the consumer because the generators intercept its calls
  Qyl.Xml/                            the XML contract as an assembly: [GenerateXml], IXmlWritable, XmlShape, XmlHttpResult
  Qyl.Api/                            the runtime as an assembly: IQylApiBuilder, QylResults, problem-details JSON, XML schema transformer
  Qyl.Sdk.Xml.Generator/              Roslyn incremental generator behind [GenerateXml]: parser → cacheable tree spec → emitter
  Qyl.Sdk.Xml.Generator.Tests/        XmlSerializer parity, pinned snapshots, diagnostics, incremental caching (dotnet test)
  DECISIONS.md                        dated decisions with reasons
qyl.sample/
  Program.cs                          the composition root above
  Todos/                              Todo (template model, now partial + XML-mapped), CreateTodoRequest, TodoStore, TodoEndpoints
  openapi/qyl.sample.json             the committed contract
  Dockerfile                          Native AOT container build (sdk:10.0-noble-aot → runtime-deps:10.0-noble-chiseled)
verify.sh                             the proof: managed, Native AOT, container
```

The consuming project is two imports and a target framework:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
    <Import Project="../qyl.sdk/Sdk/Sdk.props" />
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <RootNamespace>Qyl.Sample</RootNamespace>
    </PropertyGroup>
    <Import Project="../qyl.sdk/Sdk/Sdk.targets" />
</Project>
```

## Overrides

There is no SDK options type. Overrides use the framework's own mechanisms, registered after `AddQylApi`:

```csharp
// Validation: any ValidationOptions member.
builder.Services.Configure<ValidationOptions>(options => options.MaxDepth = 16);

// OpenAPI: transformers, ShouldInclude, schema ids ... on the "v1" document.
builder.Services.Configure<OpenApiOptions>("v1", options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Todos";
        return Task.CompletedTask;
    }));
```

The OpenAPI *version* is the one exception, because the build-time document tool takes it as its own argument and ignores
`OpenApiOptions.OpenApiVersion`. It is the MSBuild property `QylOpenApiVersion` (default `OpenApi3_1`), which the SDK passes to
the tool and generates into the compilation; both documents move together:

```xml
<PropertyGroup>
    <QylOpenApiVersion>OpenApi3_0</QylOpenApiVersion>
</PropertyGroup>
```

Every SDK default in `Build/Qyl.Sdk.Api.props` is conditional on the property being empty, so a project or `Directory.Build.props`
that sets it first wins.

## Run the proof

```sh
./verify.sh
```

Eight stages: build into a scratch artifacts directory and run the generator tests; confirm the committed contract did not
change; confirm every generator's output; run the HTTP scenario against the managed build; publish Native AOT for the current macOS or Linux runtime identifier; run
the same scenario against the native executable and compare its `/openapi/v1.json` with the committed contract; when Docker is
available, build the container image and run the scenario against it (`SKIP_DOCKER=1` skips that stage explicitly); pack
`Qyl.Api.Sdk` and build a consumer from the package (see below).
`RUNTIME_ID=linux-x64 ./verify.sh` overrides platform detection; `KEEP_ARTIFACTS=1` keeps the scratch directory.

Run it by hand with `dotnet run --project qyl.sample` and [`qyl.sample/qyl.sample.http`](qyl.sample/qyl.sample.http), or
`docker compose up --build` for the container on `http://localhost:5062`.

## Decisions

Every non-obvious choice (source-compiled SDK, problem details, the OpenAPI version in MSBuild, .NET 11 features left out, the
single-call API) is recorded with date and reason in [`qyl.sdk/DECISIONS.md`](qyl.sdk/DECISIONS.md).

## Versions

- `global.json` pins SDK `10.0.400` (`rollForward: latestFeature`); Roslyn 5.9.0 for the generator project matches that compiler.
- `Qyl.Sdk.Packages.props` owns `Microsoft.AspNetCore.OpenApi` / `Microsoft.Extensions.ApiDescription.Server` 10.0.11 and pins
  `Microsoft.OpenApi` 2.12.2 transitively.
- Upstream patterns are verified against the installed SDK, not against documentation; `verify.sh` is the source of truth for
  what this repository does.

## `Qyl.Api.Sdk`, the package

`qyl.sdk/Qyl.Api.Sdk.csproj` packs the same files as an MSBuild SDK (the `ANcpLua.NET.Sdk.Web` layout): `Sdk/*` as entry points,
`Build/*` as-is, `Sources/**` (the one `AddQylApi` file) as content the props keep linking into the consumer, `Qyl.Xml.dll` and
`Qyl.Api.dll` under `lib/net10.0` as references, the generator under `analyzers/dotnet/cs`.

```sh
dotnet pack qyl.sdk/Qyl.Api.Sdk.csproj -o ./feed
```

A consumer then needs no imports, no generator reference, and no central package management:

```xml
<Project Sdk="Qyl.Api.Sdk/0.1.0-alpha">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <RootNamespace>Qyl.Sample</RootNamespace>
    </PropertyGroup>
</Project>
```

`Program.cs` is unchanged. `verify.sh` stage 8 packs the SDK into a scratch feed, builds exactly this consumer from it with the
sample's sources, and fails unless the contract it produces is identical to the committed one. The sample in this repository
keeps the explicit imports so a fresh clone builds without a pack step.

The id is `Qyl.Api.Sdk`: `Qyl.Sdk` on nuget.org is qyl's telemetry onboarding package (`builder.AddQyl()`), a different thing.
This alpha is local-only; nothing is pushed.
