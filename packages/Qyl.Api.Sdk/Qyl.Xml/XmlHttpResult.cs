using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace Qyl.Xml;

/// <summary>
/// An <see cref="IResult"/> that writes a generated XML document as <c>200 application/xml</c>. Create one with
/// <c>QylResults.Xml(value)</c>; it declares its response metadata, including the document's <see cref="XmlShape"/>, so the OpenAPI
/// document and the wire agree.
/// </summary>
/// <typeparam name="TValue">The document type; a <c>[GenerateXml]</c> type in practice.</typeparam>
public sealed class XmlHttpResult<TValue> : IResult, IEndpointMetadataProvider, IStatusCodeHttpResult, IContentTypeHttpResult, IValueHttpResult, IValueHttpResult<TValue>
    where TValue : IXmlWritable
{
    /// <summary>The media type written, including the charset the pipeline encodes with.</summary>
    public const string MediaType = "application/xml; charset=utf-8";

    /// <summary>Creates the result for <paramref name="value"/>.</summary>
    /// <param name="value">The document to write.</param>
    public XmlHttpResult(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

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
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Encoded once, straight to UTF-8: XmlWriter writes synchronously, and the response body accepts only asynchronous writes.
        using var buffer = new MemoryStream();
        Value.WriteXmlTo(buffer);

        var response = httpContext.Response;
        response.StatusCode = StatusCode;
        response.ContentType = ContentType;
        response.ContentLength = buffer.Length;

        await response.Body.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), httpContext.RequestAborted).ConfigureAwait(false);
    }

    // Implemented explicitly on purpose: without dynamic code, Results<...> locates this method by its explicit-interface name.
    static void IEndpointMetadataProvider.PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        // The response type stays string so the OpenAPI generator never needs JSON type information for an XML-only model;
        // the XML shape below replaces the string schema in the document.
        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(string), ["application/xml"]));
        builder.Metadata.Add(new XmlResponseMetadata(StatusCodes.Status200OK, TValue.XmlShape));
    }
}
