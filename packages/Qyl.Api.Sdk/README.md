# Qyl.Api.Sdk

An MSBuild SDK for a NativeAOT ASP.NET Core API that is validated, documented and instrumented
from the first line. It carries the Native AOT template, compile-time validation, problem
details, the committed OpenAPI document, `[GenerateXml]` XML output, and qyl telemetry, behind one
call.

It is an SDK, not a package reference:

```xml
<Project Sdk="Qyl.Api.Sdk/<version>">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

builder.AddQylApi(AppJsonSerializerContext.Default);
builder.Services.AddSingleton<TodoStore>();

var app = builder.Build();
app.MapTodos();
app.Run();
```

`AddQylApi` extends `IHostApplicationBuilder`. In a fixed order it registers the JSON serializer
contexts, the problem-details context, validation, the `v1` OpenAPI document, and last `AddQyl()`,
which wires automatic instrumentation, ASP.NET Core spans and OTLP export. Reflection is off under
NativeAOT, so the serializer context is a parameter rather than a setting. Overrides go through the
framework's own options, `Configure<ValidationOptions>` and `Configure<OpenApiOptions>("v1", …)`,
after the call, with one exception: the OpenAPI specification version is the MSBuild property
`QylOpenApiVersion` (`OpenApi2_0`, `OpenApi3_0` or the default `OpenApi3_1`), because the
build-time document tool takes it as its own argument. Any other value fails the build with
`QYLSDK0003`.

## What the build does

- The OpenAPI document is written at build time and committed. On the very first build of a new
  project the document is written and the build fails with `QYLSDK0001` asking for a commit; the
  second build succeeds, and from then on a gate fails if the served and committed documents ever
  differ.
- Every span the API emits carries the revision of the contract it was built from, on the
  resource. A `baggage: session.id=<id>` request header becomes `session.id` on the server span
  and every descendant in the process.
- `[GenerateXml]` generates an `XmlWriter` implementation for a type, with no `XmlSerializer` and
  no runtime reflection, and `QylResults.Xml(todo)` puts the XML shape into the contract.

## MSBuild names

A name says what it governs. The SDK's own wiring is `QylApiSdk*`, its files are
`Qyl.Api.Sdk.*`, and a knob of the OpenAPI document is `QylOpenApi*`, after the
`OpenApi*` property of `Microsoft.Extensions.ApiDescription.Server` it feeds. A name that starts
with `_` is private and can change in any release.

| Property | Governs | Default |
| --- | --- | --- |
| `QylOpenApiVersion` | the document's OpenAPI version, passed to the build-time tool as `--openapi-version` and compiled in as `QylApiSdkBuild.OpenApiVersion` | `OpenApi3_1` |
| `QylOpenApiDocumentsDirectory` | where the committed document is written; `OpenApiDocumentsDirectory` always follows it | `$(MSBuildProjectDirectory)/openapi` |
| `QylOpenApiDocumentFile` | the document the contract revision is hashed from | `$(QylOpenApiDocumentsDirectory)/$(MSBuildProjectName).json` |
| `QylApiSdkPackaged` | `true` when the SDK runs from its package (assemblies and generator from the package) rather than from this repository (project references) | detected |
| `QylApiSdkRoot`, `QylApiSdkSourcesPath`, `QylApiSdkXmlProject`, `QylApiSdkApiProject`, `QylApiSdkXmlGeneratorProject` | where the SDK and its parts are; read them, don't set them | set by the SDK |
| `UsingQylApiSdk` | `true` in every project the SDK builds, in the `Using<Sdk>` form of `UsingMicrosoftNETSdk` | `true` |

The files are `Sdk/Sdk.props` and `Sdk/Sdk.targets` (the entry points), `Build/Qyl.Api.Sdk.props`,
`Build/Qyl.Api.Sdk.targets` and `Build/Qyl.Api.Sdk.Packages.props`, which a repository with
central package management imports from its `Directory.Packages.props`. The generator is
`Qyl.Api.Sdk.Xml.Generator`, and the class compiled into every API is `Qyl.QylApiSdkBuild`.
`QylAspNetCoreOpenApiVersion`, `QylApiDescriptionServerVersion`, `QylOpenApiModelVersion` and
`QylTelemetryVersion` are the dependency versions the SDK pins. Each names the package it pins, and
the SDK writes them into the package. Diagnostic IDs (`QYLSDK0001`–`QYLSDK0003`, `QYLXML001`–`QYLXML011`)
never change once they ship, because a consumer's `NoWarn` names them.

## More

- The worked example: [`samples/qyl.sample`](https://github.com/ANcpLua/qyl/tree/main/samples/qyl.sample)
- Documentation: [qyl.at/docs/api-sdk](https://qyl.at/docs/api-sdk/)
- The collector the telemetry lands in: [`qyl`](https://www.nuget.org/packages/qyl)
- Changes per version: [CHANGELOG](https://github.com/ANcpLua/qyl/blob/main/CHANGELOG.md)
