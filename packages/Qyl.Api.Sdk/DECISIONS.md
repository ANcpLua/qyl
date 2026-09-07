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

## 2026-09-07 · A Qyl API describes itself and observes itself; the agent is its first consumer

`AddQylApi` and `AddQyl` were disjoint: an API built by this SDK described itself perfectly and could not be
watched at all. Every argument for making validation mandatory applies to telemetry — an API nobody can observe
is not finished, it is undelivered — so the fusion registers both from one call and offers no way to have one
without the other.

Decision, in five parts.

**One call on the host builder.** `AddQylApi` extends `IHostApplicationBuilder`, not `IServiceCollection`, and
registers, in a fixed order: the JSON contexts, validation, problem details, the `v1` OpenAPI document with its
XML shapes, and `AddQyl()`. The `IServiceCollection` overload is deleted, not kept beside it; the sample's only
change is dropping `.Services`. `Qyl.Telemetry.Hosting` is pinned by the SDK through `QylTelemetryVersion` and
pulled implicitly, with its interceptor generator arriving as an analyzer through the package's own
`buildTransitive` assets — the same shape as the `[GenerateXml]` generator. The telemetry family is *not* folded
into `Qyl.Api.dll`: its major is a compile-time ABI on its own release line, and a package boundary is what lets
the two move independently.

**Telemetry is mandatory, with one exception that is not an opt-out.** There is no `WithTelemetry`, no options
flag, no environment switch. The single conditional is `QylBuildTimeDocumentHost`: under
`GetDocument.Insider`, the process that `Microsoft.Extensions.ApiDescription.Server` builds the host in and
never starts, collector discovery is off. Discovery is four blocking TCP probes and a DNS lookup; paying them on
every `dotnet build`, for telemetry that cannot be emitted because the host never runs, is waste — and on a
machine with a collector up it would make the build's behaviour depend on it. Nothing else about `AddQyl()`
changes there. Verified with a listener on 127.0.0.1:4318 and :4317 during a full `dotnet build` of the sample:
zero connections, while starting the same binary for real connects immediately.

**The agent contract is a header, not an API.** `Qyl.Api` reads the W3C `baggage` request header's `session.id`
member and stamps it on the server span; `QylSessionSpanProcessor` already carries a tag from an in-process
ancestor to its descendants, so one header groups everything the request caused. An agent sends
`baggage: session.id=<id>` and afterwards finds its own run through MCP `list_sessions` / `get_trace`. The value
is percent-decoded, capped at 128 characters and rejected if it carries a control character, because it becomes
a storage key in the collector and a path segment in its read API; a longer id is ignored rather than truncated,
since a truncated id would silently merge two agents' work. A request without the header is left alone — nothing
is invented, and an untagged trace is grouped by trace id as before.

The filter is registered *after* `AddQyl()`, which reads oddly and is load-bearing: `IStartupFilter`s wrap the
pipeline in registration order, outermost first, and the server span being tagged is created by the middleware
`AddQyl()`'s own filter installs. Registered ahead of it, this filter would run outside that span.

**Every span names its contract.** The SHA-256 of the committed OpenAPI document, lowercase hex, is computed by
MSBuild (`GetFileHash` in `Qyl.Sdk.Api.targets`) and written into the compilation as
`QylSdkBuild.ContractRevision` by the same mechanism that already wrote `QylSdkBuild.OpenApiVersion`;
`AddQylApi` exports it as the resource attribute `qyl.api.contract.revision`. It is MSBuild's to compute rather
than the process's: a binary must report the contract it was compiled against, not one it reads off disk at
start-up and can be lied to about. On a project's very first build no document is committed yet and the constant
is empty — that is a fact about that binary, not a case to branch on, so the attribute is exported either way.

**The proof gains a stage.** `ApiSdkSessionScenario` starts this repository's collector on free loopback ports,
runs the Native AOT sample against it, drives three requests under one `baggage: session.id=…`, and asserts
through the collector's own read API (`GET /api/v1/sessions/{id}/traces`, what MCP serves an agent) that the
session holds exactly those three routed server spans — `/todos/` 400, `/todos/` 201, `/todos/{id:int}/xml` 200
— and that every span in it carries the contract revision. The sample still contains no telemetry line; if the
stage passes, the one call is the reason.

Open, and owed by this wave: `qyl.api.contract.revision` is in the collector's
`qylResourceAttributeAllowList` (`eng/config/collector-semantic-policy.json`) but is a string literal in
`Qyl.Api/QylApiContract.cs`, because it is registered in the Weaver registry only from
`Qyl.Telemetry.SemanticConventions` 9.2.0. When that version is on nuget.org the pin in `Version.props` moves
and the literal becomes `QylAttributes.ApiContractRevision`. Until then
`BuildCollectorSemanticCatalog.cs` writes `qylResourceAttributeAllowList` raw, unlike `sessionCorrelation` and
the denial lists, which it resolves against the packages — that list gets the same
`RequiredAttributeValues` validation with the pin bump, not before, because a gate that cannot pass is a gate
that stops the catalog being regenerated at all.

## 2026-09-07 · `qyl.api.contract.revision` is the registry's name, and the allow list is held to the registry

Closes the "Open" paragraph of the entry above, in the same wave rather than after it.
`Qyl.Telemetry.SemanticConventions` 9.2.0 registers `qyl.api.contract.revision` in the Weaver registry, so the
pin in `Version.props` moves to 9.2.0 and `QylApiContract.RevisionAttributeName` is
`QylAttributes.ApiContractRevision` from `Qyl.Telemetry.SemanticConventions.Incubating` — a compile-time
`PrivateAssets="all"` reference consumed as a `const`, which puts a new row in the §2 edge table
(`eng/build/BuildDependencyEdges.cs`) and nothing new in a consumer's output.

With the key registered, `BuildCollectorSemanticCatalog` now resolves `qylResourceAttributeAllowList` through
`RequiredAttributeValues`, as it already did for `sessionCorrelation` and the denial lists: a qyl-owned key the
collector persists must exist in the pinned packages, or the vocabulary and the storage policy have drifted.
That validation was deliberately not added before the pin — a gate that cannot pass does not protect the
invariant, it stops the catalog being regenerated at all.

## 2026-09-07 · The revision value is `sha256:<hex>`, and the gate stops shadowing the published package

Two corrections from the refutation of the fusion, folded in before merge rather than filed.

**The value carries its algorithm.** The first cut emitted a bare lowercase hex digest. The registry row for
`qyl.api.contract.revision` defines the value as `sha256:<hex>` — "SHA-256 of the committed OpenAPI document
the API was built from, as `sha256:<hex>`" — and the collector already reports its own revision that way from
`/health`. A digest without its algorithm is a value that has to be reinterpreted the day the algorithm
changes, instead of simply differing. `Qyl.Sdk.Api.targets` now writes the prefix.

The gate could not have caught this: `ApiSdkSessionScenario`'s oracle hashed the same file the same way, so
both sides were wrong together. The oracle now spells the format itself (`ContractRevisionValue`), so the
assertion compares two independent statements of the value rather than one statement with itself, and
`ContractRevisionTests` pins the shape in a third place — the only three that can disagree.

**The proof cleans up after itself.** `ApiSdkPackagedConsumer` packs a `Qyl.Api.Sdk` with the repository's
current version and drops that version from the global packages folder so the consumer resolves it. It only
did so *before* the build, so the run ended with a gate artifact sitting under the published package's id and
version — different content, same coordinates — which every later restore on the machine, in any repository,
would silently prefer. The purge now also runs in a `finally`, so a failed stage leaves nothing behind either,
and the stage asserts the directory is gone before it reports success.

## 2026-09-07 · The fusion is 5.0.0, not 4.1.0

`AddQylApi` moved from `IServiceCollection` to `IHostApplicationBuilder` and the old overload was deleted
rather than kept beside it, so every existing Qyl API fails to compile until one line changes. That is a
major by the only definition that matters to a consumer, whatever else the release carries; a minor would
have said "your code still builds" and it does not.

`QylVersion` in `Version.props` is therefore 5.0.0, and everything derived from it moves with it in this one
edit: the packed `Qyl.Api.Sdk` and the `qyl` tool, the `<Project Sdk="Qyl.Api.Sdk/5.0.0">` line the README
documents and the one `Sdk/Sdk.props` names in its header comment, the CHANGELOG heading, and the version the
`ApiSdkPackagedConsumer` gate packs and resolves — that last one reads `QylVersion` directly, so it needed no
edit and could not have been forgotten. The earlier entries above cite `Qyl.Api.Sdk/4.0.0` because that is
what they verified at the time; they are history and stay as written.
