# qyl.sdk decisions

One entry per decision, dated, with the reason. A decision is reversed by a new entry, not by editing an old one.

## 2026-09-04 · The SDK compiles into the consumer as source, not as a DLL

The .NET 10 validation generator (`Microsoft.Extensions.Validation`) and the OpenAPI XML-comment generator
(`Microsoft.AspNetCore.OpenApi`) intercept the `AddValidation()` / `AddOpenApi()` call inside the compilation that makes it, and
discover validatable types and XML comments from that compilation only. Verified against the installed SDK 10.0.400: the
interceptor attributes in `obj/generated` point at `QylApiServiceCollectionExtensions.cs`, a linked SDK source file. A compiled
`Qyl.Sdk.dll` calling the same methods would validate nothing and document nothing. `Build/Qyl.Sdk.Api.props` therefore links
`Sources/**/*.cs` into every consumer.

## 2026-09-04 · `AddQylApi` also registers problem details

Without an `IProblemDetailsService`, the .NET 10 validation filter (`ValidationEndpointFilterFactory`) returns the raw
`HttpValidationProblemDetails` object, which the pipeline writes as `application/json`; the endpoint metadata from
`ProducesValidationProblem()` says `application/problem+json`. With the service registered the wire matches the contract.
The SDK additionally appends `QylProblemJsonContext` to the JSON resolver chain so the fallback path (a client whose `Accept`
excludes JSON) serializes without reflection under Native AOT. Both paths are exercised by `verify.sh`.

## 2026-09-04 · OpenAPI 3.1, pinned once, in code

.NET 10 defaults to OpenAPI 3.1; .NET 11 moves the default to 3.2. `AddQylApi` sets `OpenApiSpecVersion.OpenApi3_1` explicitly.
The build-time document (`Microsoft.Extensions.ApiDescription.Server`) runs the app's own options, so the committed contract and
the runtime document agree by construction; there is no second `--openapi-version` in MSBuild to keep in sync.
Override per document with `services.Configure<OpenApiOptions>("v1", o => o.OpenApiVersion = ...)` after `AddQylApi`.

## 2026-09-04 · .NET 11 preview 7 features are not emulated on .NET 10

Checked against the .NET 11 preview 7 notes and the ASP.NET Core docs dated 2026-08-26 and adapted to what SDK 10.0.400 ships.
Deliberately absent until the SDK targets .NET 11: async validation (`AsyncValidationAttribute`, `IAsyncValidatableObject`),
built-in validation localization, OpenAPI 3.2 by default, `[ValidatableType]` leaving experimental status (`ASP0029` on .NET 10).
Upstream patterns are always verified against the installed SDK, never against documentation alone.

## 2026-09-04 · One call, no `With*` chain

The first draft exposed `AddQylApi().WithJson(ctx).WithValidation().WithOpenApi()`, imitating
`AddOpenTelemetry().WithTracing().UseAzureMonitorExporter()`. That chain exists upstream because each step is a real choice
(which signals, which exporter); in the Azure Functions sample `UseFunctionsWorkerDefaults()` is the bundle and
`UseAzureMonitorExporter()` the choice. In a Qyl API none of the three steps is a choice: without a JSON context the API does not
run (reflection is off), without validation it is not a Qyl API, without OpenAPI there is no contract. Presenting mandatory steps as
options is the opposite of opinionated.

Decision: `builder.Services.AddQylApi(AppJsonSerializerContext.Default)` does everything, in a fixed order (consumer contexts,
problem context, validation, problem details, OpenAPI). `IQylApiBuilder` stays as the return type, empty, reserved for genuine
future choices such as a telemetry exporter. Overrides go through the framework's own `Configure<ValidationOptions>` and
`Configure<OpenApiOptions>("v1", ...)`; there is no SDK options type. The literal `AddValidation()` and `AddOpenApi(lambda)` calls
stay in the method body so both generators keep intercepting. Side effect: the resolver order is fixed in one place instead of
depending on call order.

## 2026-09-04 · The OpenAPI version lives in MSBuild, not in code (supersedes "pinned once, in code")

Verified against SDK 10.0.400 with a `Configure<OpenApiOptions>("v1", o => o.OpenApiVersion = OpenApi3_0)` override: the served
document became 3.0.4, the build-time document stayed 3.1.1. `Microsoft.Extensions.ApiDescription.Server` calls the
`GenerateAsync` overload that takes the version as its own argument (the tool logs "Using discovered `GenerateAsync` overload
with version parameter") and ignores the app's options. A code constant therefore cannot be the single source of truth.

Decision: the MSBuild property `QylOpenApiVersion` (default `OpenApi3_1`) is passed to the tool as `--openapi-version` and
written into the compilation as `Qyl.QylSdkBuild.OpenApiVersion` before `CoreCompile`; `AddQylApi` applies that constant. A
consumer overrides the version with `<QylOpenApiVersion>OpenApi3_0</QylOpenApiVersion>` and both documents move together.
`Configure<OpenApiOptions>("v1", ...)` remains the mechanism for everything else (transformers, `ShouldInclude`, ...);
`verify.sh` fails if the served document ever differs from the committed one.
