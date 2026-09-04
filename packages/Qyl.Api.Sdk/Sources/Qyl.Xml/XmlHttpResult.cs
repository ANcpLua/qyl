using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;

namespace Qyl.Xml;

/// <summary>
/// An <see cref="IResult"/> that writes a generated XML document as <c>200 application/xml</c>. Create one with
/// <see cref="QylResults.Xml{TValue}(TValue)"/>; it declares its response metadata so the OpenAPI document and the wire agree.
/// </summary>
/// <typeparam name="TValue">The document type; a <c>[GenerateXml]</c> type in practice.</typeparam>
public sealed class XmlHttpResult<TValue> : IResult, IEndpointMetadataProvider, IStatusCodeHttpResult, IContentTypeHttpResult, IValueHttpResult, IValueHttpResult<TValue>
    where TValue : IXmlWritable
{
    /// <summary>The media type written, including the charset the pipeline encodes with.</summary>
    public const string MediaType = "application/xml; charset=utf-8";

    internal XmlHttpResult(TValue value)
    {
        Value = value;
    }

    /// <summary>The document being written.</summary>
    public TValue Value { get; }

    object? IValueHttpResult.Value => Value;

    /// <summary>Always <see cref="StatusCodes.Status200OK"/>.</summary>
    public int StatusCode => StatusCodes.Status200OK;

    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    /// <summary>Always <see cref="MediaType"/>.</summary>
    public string ContentType => MediaType;

    string? IContentTypeHttpResult.ContentType => ContentType;

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = StatusCode;
        httpContext.Response.ContentType = ContentType;

        return httpContext.Response.WriteAsync(Value.ToXml(), httpContext.RequestAborted);
    }

    // Implemented explicitly on purpose: without dynamic code, Results<...> locates this method by its explicit-interface name.
    static void IEndpointMetadataProvider.PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(string), ["application/xml"]));
    }
}
