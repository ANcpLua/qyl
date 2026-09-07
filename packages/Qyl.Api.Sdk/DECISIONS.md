# Qyl.Api.Sdk decisions

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
excludes JSON) serializes without reflection under Native AOT. Both paths are exercised by the `ApiSdk` gate.

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
`ApiSdkContractIsCommitted` fails if the served document ever differs from the committed one.

## 2026-09-04 · Packed as an MSBuild SDK; the sample keeps explicit imports

`Qyl.Api.Sdk.csproj` packs `Sdk/`, `Build/`, `Sources/**` and the generator (`analyzers/dotnet/cs`) as the
`Qyl.Sdk` MSBuild SDK (`packageType` `MSBuildSdk`). The targets detect the packed form by the presence of that analyzer and switch
from the `ProjectReference` to an `<Analyzer>`; implicit package references carry `Version` without CPM and `VersionOverride`
with it. Verified by `ApiSdkPackagedConsumer`: a consumer reading `<Project Sdk="Qyl.Api.Sdk/4.0.0">` from a scratch feed, with
the sample's sources and an unchanged `Program.cs`, produces the byte-identical contract.

The sample in this repository stays on the explicit `Sdk.props` / `Sdk.targets` imports: a fresh clone must build in Rider or
with `dotnet build` without a pack step and without a local feed.

## 2026-09-07 · The XML generator writes an element tree; parity with `XmlSerializer` or a compile error

The first generator wrote flat models only and, measured against `XmlSerializer` for the same attributes, differed silently in
three places: `Namespace = ""` on a member collapsed to "inherit", `[Flags]` enums and `[XmlEnum]` were ignored in favour of
`Enum.ToString()`, and base-class properties were dropped. That contradicts the promise in `[GenerateXml]`'s documentation.

Decision: the pipeline keeps its three stages (parser → value-equal spec → emitter, so unrelated edits stay cached), but the spec
is a tree. A property is an attribute, an element, text, a nested `[GenerateXml]` model written under the parent's chosen name, or a
collection of scalars or models with or without an `[XmlArray]` wrapper. `IXmlWritable` gains `WriteXml(writer, localName, ns)` for
that nesting and a `static abstract RootElement` naming the document element. Enums become a generated `switch` that honours
`[XmlEnum]` and writes flags the way `XmlSerializer` does (exact member first, then set members separated by spaces). Absent
`Nullable<T>` elements and absent wrapped items are written as `xsi:nil`, absent strings and models are omitted, `IsNullable` on
`XmlElement`, `XmlArray`, and `XmlArrayItem` overrides that. Base classes declared in the compilation are written first.

Rule: every `System.Xml.Serialization` option is either implemented identically to `XmlSerializer` or a diagnostic (QYLXML006,
008–011), including `XmlType`, `[DefaultValue]`, `ShouldSerializeX()`, and `XSpecified`. `Qyl.Sdk.Xml.Generator.Tests` enforces it:
each model is serialized by the generated code and by `XmlSerializer` and the strings must be identical; a snapshot pins the
generated source; a tracked-step test proves an edit elsewhere leaves the model and the output cached. Two deliberate extensions
where `XmlSerializer` throws instead: read-only properties are written, and `Nullable<T>` attributes are omitted when absent.

Open: the runtime contract (`IXmlWritable`, `[GenerateXml]`, `XmlHttpResult`) is still linked as source into every consumer, so a
model library and its API would each carry their own copy of the interface. The interceptor argument for source linking only
applies to `AddQylApi`; moving `Qyl.Xml` into a compiled assembly is a packaging decision for after this alpha.


## 2026-09-07 · The XML contract is the compiled `Qyl.Xml` assembly; the package id is `Qyl.Api.Sdk`

Supersedes "as source, not as a DLL" for the XML contract only. That entry's reason, the interceptors behind `AddValidation()` and
`AddOpenApi()`, applies to `AddQylApi` and what it registers; those stay in `Sources/` and compile into the consumer. `[GenerateXml]`,
`IXmlWritable`, `XmlShape`, and `XmlHttpResult` do not intercept anything, and as linked source every consumer assembly carried its
own copy of the interface, so a model library and the API serving it could never share one. `Qyl.Xml/` is now a net10.0
assembly depending on the shared framework only; the targets reference it as a project in the repository and as
`lib/net10.0/Qyl.Xml.dll` from the package, next to the generator under `analyzers/dotnet/cs`.

Decision, package id: `Qyl.Api.Sdk`. `Qyl.Sdk` on nuget.org stays qyl's telemetry onboarding package. The pack project carries
the new name; the MSBuild file names under `Build/` and the generator's name are internal and unchanged.
`ApiSdkPackagedConsumer` builds the consumer from `<Project Sdk="Qyl.Api.Sdk/4.0.0">` and checks that `Qyl.Xml.dll` reached its
output.

## 2026-09-07 · An `application/xml` response is described in the contract from the generated shape

The contract said `type: string` for `GET /todos/{id}/xml`. The generator now emits, next to the writer, the same tree as data:
`XmlShape`, with every node's XML name, namespace, scalar type, optionality, and nil behaviour, and enum names for non-flags enums.
`XmlHttpResult<T>` adds it as `XmlResponseMetadata` to the endpoint; `AddQylApi` registers an operation transformer that replaces
the string schema with a component `{TypeName}Xml` built from that data: `xml.name` and `xml.namespace` on the object, `xml.attribute`
on attributes, `xml.wrapped` arrays with named items, nested models as `allOf` references so a model that refers to itself
resolves, `type: [..., "null"]` where an absent value is written as `xsi:nil`, and `#text` for text content, which OpenAPI has no
keyword for. The response type declared to ASP.NET Core stays `string` on purpose: the OpenAPI generator therefore never needs JSON
type information for an XML-only model, which under `JsonSerializerIsReflectionEnabledByDefault=false` would fail the document.
No reflection is involved at any point; the build-time and the runtime document are produced by the same transformer.

## 2026-09-07 · Only `AddQylApi` is linked source; the runtime is the compiled `Qyl.Api` assembly

Supersedes "as source, not as a DLL" (2026-09-04) for everything except one file. That entry's reason was correct and still
holds, but it was applied too broadly: the interceptors need the literal `AddValidation()` and `AddOpenApi(lambda)` calls in the
consumer's compilation, and those calls sit in `QylApiServiceCollectionExtensions.cs`. Nothing else in `Sources/` depended on
being compiled by the consumer. `IQylApiBuilder`, `QylResults`, `QylProblemJsonContext`, and the XML schema transformer are now
`Qyl.Api/`, a net10.0 assembly that references `Qyl.Xml` and the pinned `Microsoft.AspNetCore.OpenApi`. `Sources/`
contains exactly the file that must be linked, and the props say why.

Consequences: the builder class and the problem-details context are public, because the linked `AddQylApi` creates and registers
them from another assembly. The package ships `lib/net10.0/Qyl.Api.dll` next to `Qyl.Xml.dll`; the targets reference both, as
projects in the repository and as assemblies from the package. `ApiSdkPackagedConsumer` checks that both reached the consumer's
output.

## 2026-09-07 · The SDK ships from the qyl repository, on the qyl version line

This SDK was its own repository, with its own build system, its own shell gate suite and a local-only
alpha version that was never pushed. One artifact set does not need two of any of those, so the
history moved into `github.com/ANcpLua/qyl`: this directory is `packages/Qyl.Api.Sdk/`, the sample that
proves it is `samples/qyl.sample/`, and both build with that repository's `Directory.Build.props`,
`Directory.Packages.props` and analyzer settings. Every entry above still holds; the paths and gate names
they cite are the ones this tree has.

The version is the repository's: `QylVersion` in `Version.props` makes this `Qyl.Api.Sdk 4.0.0`, published by
the same trusted-publishing workflow as the `qyl` tool. The project packs itself instead of listing files in
a nuspec, and resolves the two libraries and the analyzer from the referenced projects' build output rather
than from `bin/<configuration>/<tfm>` paths, which that repository's artifacts layout does not have. The packed
`Build/Qyl.Sdk.Packages.props` is written at pack time with its versions resolved, because the committed one
reads them from a `Version.props` a consumer does not have.

The eight-stage proof is `eng/build/BuildApiSdk.cs`, reachable from the `Ci` target and run by a CI job, in
the same order and asserting the same things as the shell script it replaces — with the HTTP scenario written
against the .NET stacks rather than curl, jq and xmllint, so a missing shell tool fails nothing silently.

`Microsoft.OpenApi` is now referenced directly rather than pinned transitively: a consumer without central
package management resolved 2.7.5, the low end of the range `Microsoft.AspNetCore.OpenApi` accepts, against a
`Qyl.Api` compiled for 2.12.2.

## 2026-09-07 · Facts behind the XML parity and the proof, recorded so they are not rediscovered

Not decisions themselves, but each shaped one above and would cost a scratch project or a failed proof run to re-derive.

`XmlSerializer` on SDK 10.0.400, verified with a scratch console app: an absent `Nullable<T>` element and an absent item of an
`[XmlArray]` collection are written as an empty element with `xsi:nil="true"` under a writer-generated prefix (`p2`, `p3`, by
depth); an absent string, class member, or `[XmlText]` writes nothing; an absent item of an `[XmlElement]`-flattened collection
is skipped; `IsNullable` on `XmlElement`, `XmlArray`, and `XmlArrayItem` overrides each of these. `[Flags]` enums first match one
member exactly, so a composite member such as `All = 7` wins over its parts, then the set members are joined with spaces in
declaration order; `[XmlEnum]` renames; an unknown value throws "Instance validation error: '{value}' is not a valid value for
{ShortName}.". `DateOnly` is written as `yyyy-MM-dd`, `TimeOnly` as `HH:mm:ss.FFFFFFF`. A `Nullable<T>` attribute and a positional
record throw during reflection. The generator matches all of it; the two places where it deliberately does more are recorded above
(read-only properties are written, `Nullable<T>` attributes are omitted when absent).

On the wire: a todo without a due date is now `<due-by p2:nil="true" xmlns:p2="http://www.w3.org/2001/XMLSchema-instance" />`
instead of no element; `[XmlElement("due-by", IsNullable = false)]` restores the omission. In the OpenAPI schema, text content is
the property `#text`, because the `xml` object of OpenAPI has no keyword for it.

Build and proof: the generator's `ProjectReference` carries no `SetTargetFramework`; on a single-target project it creates a
second project instance whose `--no-incremental` rebuild deletes the first instance's output while another referrer (the tests)
compiles against it. A class library writes `obj/generated` only if it sets `EmitCompilerGeneratedFiles` itself, which `Qyl.Api`
does for the problem-details JSON context.
