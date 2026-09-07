using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Validation;
using Microsoft.OpenApi;
using Qyl;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Extension methods for setting up a Qyl API in an <see cref="IServiceCollection"/>.</summary>
public static class QylApiServiceCollectionExtensions
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
    /// <param name="services">The service collection of the host.</param>
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
    /// </list>
    /// <para>
    /// The generators behind steps 2 and 4 intercept the literal <c>AddValidation()</c> and <c>AddOpenApi(lambda)</c> calls in this
    /// method. That works because the SDK compiles into the consumer; the calls are in the consumer's compilation, whichever method
    /// they sit in. Overrides use the framework's own mechanisms: <c>Configure&lt;ValidationOptions&gt;</c> and
    /// <c>Configure&lt;OpenApiOptions&gt;("v1", ...)</c>, registered after this call; the document version alone is the MSBuild
    /// property <c>QylOpenApiVersion</c>, for the reason given on <see cref="OpenApiDocumentVersion"/>.
    /// </para>
    /// </remarks>
    public static IQylApiBuilder AddQylApi(this IServiceCollection services, params JsonSerializerContext[] contexts)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contexts);

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

        return new QylApiBuilder(services);
    }
}
