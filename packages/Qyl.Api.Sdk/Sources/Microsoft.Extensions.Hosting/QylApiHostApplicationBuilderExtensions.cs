using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Validation;
using Microsoft.OpenApi;
using Qyl;

namespace Microsoft.Extensions.Hosting;

/// <summary>Extension methods for setting up a Qyl API on an <see cref="IHostApplicationBuilder"/>.</summary>
// CA1515 reads this file as application code because it is compiled into the application. It is
// the SDK's entry point, public in every consumer by design; the validation, OpenAPI and
// auto-instrumentation generators only intercept the calls it makes when it is compiled there,
// which is why it ships as source rather than in Qyl.Api.dll.
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "The SDK's public entry point, linked into the consumer's compilation on purpose.")]
public static class QylApiHostApplicationBuilderExtensions
{
    /// <summary>The name of the OpenAPI document every Qyl API serves and commits.</summary>
    public const string OpenApiDocumentName = "v1";

    /// <summary>
    /// The OpenAPI specification version every Qyl API document is written in, at runtime and at build time alike:
    /// the MSBuild property <c>QylOpenApiVersion</c> (default <c>OpenApi3_1</c>), generated into <see cref="QylSdkBuild"/>.
    /// </summary>
    /// <remarks>
    /// Pinned on purpose: .NET 10 defaults to 3.1 and .NET 11 moves the default to 3.2. A committed contract must not change
    /// because the framework's default did. It is an MSBuild property rather than a code constant because the build-time
    /// document tool takes the version as its own argument and ignores <see cref="OpenApiOptions.OpenApiVersion"/>; one property
    /// feeds both. Overriding it at runtime alone would make the served document diverge from the committed one.
    /// </remarks>
    public const OpenApiSpecVersion OpenApiDocumentVersion = QylSdkBuild.OpenApiVersion;

    /// <summary>Registers everything a Qyl API consists of. There is no smaller unit.</summary>
    /// <param name="builder">The host builder of the API.</param>
    /// <param name="contexts">
    /// The source-generated <see cref="JsonSerializerContext"/> instances that describe every shape crossing the HTTP boundary,
    /// in resolution order. A Qyl API compiles with <c>JsonSerializerIsReflectionEnabledByDefault=false</c>, so a type reachable
    /// from none of them fails at runtime with <see cref="NotSupportedException"/>; that is the intended signal.
    /// </param>
    /// <returns>An <see cref="IQylApiBuilder"/>; see its remarks for what it is for.</returns>
    /// <remarks>
    /// <para>What is registered, in order:</para>
    /// <list type="number">
    /// <item><description>JSON: <paramref name="contexts"/> first, then the SDK's problem-details context, ahead of the framework's resolvers.</description></item>
    /// <item><description>Validation of every Minimal API request against its DataAnnotations, via the generated resolver.</description></item>
    /// <item><description>Problem details, so validation failures are <c>400 application/problem+json</c> and match the contract.</description></item>
    /// <item><description>The <c>v1</c> OpenAPI document, pinned to <see cref="OpenApiDocumentVersion"/>, populated from XML documentation comments, with every <c>application/xml</c> response described from its model's generated XML shape.</description></item>
    /// <item><description>Observability: <c>AddQyl()</c> — auto-instrumentation, ASP.NET Core spans, OTLP export — with the committed contract's revision on the resource, then the <c>baggage</c> request header's <c>session.id</c> member on the server span that call's own startup filter creates.</description></item>
    /// </list>
    /// <para>
    /// The generators behind steps 2 and 4 intercept the literal <c>AddValidation()</c> and <c>AddOpenApi(lambda)</c> calls in
    /// this method. That works because the SDK compiles into the consumer; the calls are in the consumer's compilation,
    /// whichever method they sit in. <c>AddQyl()</c> is not intercepted — the auto-instrumentation generator rewrites the
    /// instrumented call sites in the consumer's compilation, not the registration. Overrides use the framework's own mechanisms:
    /// <c>Configure&lt;ValidationOptions&gt;</c> and <c>Configure&lt;OpenApiOptions&gt;("v1", ...)</c>, registered after this call;
    /// the document version alone is the MSBuild property <c>QylOpenApiVersion</c>, for the reason given on
    /// <see cref="OpenApiDocumentVersion"/>.
    /// </para>
    /// <para>
    /// Telemetry is not a choice. A Qyl API that cannot be observed is not a Qyl API, so there is no <c>WithTelemetry</c> and no
    /// way to opt out, exactly as there is no way to opt out of validation. The telemetry family keeps its own release line and
    /// stays a package the SDK pins (<c>QylTelemetryVersion</c>) rather than being folded into <c>Qyl.Api.dll</c>.
    /// </para>
    /// </remarks>
    public static IQylApiBuilder AddQylApi(this IHostApplicationBuilder builder, params JsonSerializerContext[] contexts)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(contexts);

        var services = builder.Services;

        services.ConfigureHttpJsonOptions(options =>
        {
            var chain = options.SerializerOptions.TypeInfoResolverChain;
            for (var index = 0; index < contexts.Length; index++)
            {
                chain.Insert(index, contexts[index]);
            }

            chain.Insert(contexts.Length, QylProblemJsonContext.Default);
        });

        // Keep this call literal: the Microsoft.Extensions.Validation generator intercepts it and emits the resolver.
        services.AddValidation();

        // Without an IProblemDetailsService the .NET 10 validation filter returns the raw HttpValidationProblemDetails object,
        // which the pipeline writes as application/json, contradicting ProducesValidationProblem().
        services.AddProblemDetails();

        // Keep this call literal and its argument a lambda: the Microsoft.AspNetCore.OpenApi generator intercepts this overload
        // and registers the compile-time XML-comment cache.
        services.AddOpenApi(OpenApiDocumentName, options =>
        {
            options.OpenApiVersion = OpenApiDocumentVersion;

            // application/xml responses are described from the generated XmlShape, not from JSON type information.
            options.AddOperationTransformer(new QylXmlResponseTransformer());
        });

        // A binary that cannot name the contract it was built from must not run. The SDK's targets fail the build when the
        // committed document is missing or stale, so this is the case that survives a tampered-with build rather than a
        // routine one — an empty revision on the resource would be a span claiming a contract it cannot identify.
        if (QylSdkBuild.ContractRevision.Length is 0)
        {
            throw new InvalidOperationException(
                "This Qyl API was compiled without a contract revision, which means it was built without its committed " +
                "OpenAPI document. Rebuild the project; the SDK writes the document and fails the build until it is committed.");
        }

        // Unlike AddValidation and AddOpenApi above, nothing intercepts this call: the auto-instrumentation generator
        // rewrites the instrumented call sites in the compilation (database commands, forwarded client calls), not this one.
        // It is last because it is the composition step — everything above is registered by the time telemetry is wired.
        builder.AddQyl(options =>
        {
            // Under the build-time document tool this host is built and never started, so the tracer and meter
            // providers are never materialised — but the OpenTelemetry logger provider is, at Build(), and it flushes
            // into a collector on dispose. These three lines are the whole difference and none of them is an opt-out
            // for a process that serves requests. See QylBuildTimeDocumentHost.
            options.EnableCollectorDiscovery = !QylBuildTimeDocumentHost.IsCurrentProcess;
            options.EnableLogExport = !QylBuildTimeDocumentHost.IsCurrentProcess;
            options.RequireConfiguredEndpoint = QylBuildTimeDocumentHost.IsCurrentProcess;
            options.ResourceAttributes.Add(
                new KeyValuePair<string, object>(QylApiContract.RevisionAttributeName, QylSdkBuild.ContractRevision));
        });

        // The agent contract: `baggage: session.id=<id>` on the request becomes session.id on the server span, which the
        // session processor then copies to every descendant span in the process. An agent that sends the header finds
        // everything it triggered under that session in the collector.
        //
        // After AddQyl on purpose. Startup filters wrap the pipeline in registration order, outermost first, and the server
        // span this stamps is created by the filter AddQyl registers — a filter registered ahead of it would run outside the
        // span and tag the wrong activity, or none.
        services.AddSingleton<IStartupFilter, QylSessionBaggageStartupFilter>();

        return new QylApiBuilder(services);
    }
}
