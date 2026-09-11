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
after the call.

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

## More

- The worked example: [`samples/qyl.sample`](https://github.com/ANcpLua/qyl/tree/main/samples/qyl.sample)
- Documentation: [qyl.at/docs/api-sdk](https://qyl.at/docs/api-sdk/)
- The collector and dashboard the telemetry lands in: [`qyl`](https://www.nuget.org/packages/qyl)
- Changes per version: [CHANGELOG](https://github.com/ANcpLua/qyl/blob/main/CHANGELOG.md)
