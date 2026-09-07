using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Qyl.Xml;

namespace Qyl;

/// <summary>
/// Describes every <c>application/xml</c> response an endpoint declares through <see cref="XmlResponseMetadata"/> with the
/// component schema built from the model's generated <see cref="XmlShape"/>, replacing the placeholder string schema.
/// </summary>
public sealed class QylXmlResponseTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Document is not { } document)
        {
            return Task.CompletedTask;
        }

        foreach (var metadata in context.Description.ActionDescriptor.EndpointMetadata.OfType<XmlResponseMetadata>())
        {
            var statusCode = metadata.StatusCode.ToString(CultureInfo.InvariantCulture);
            if (operation.Responses is { } responses &&
                responses.TryGetValue(statusCode, out var response) &&
                response.Content is { } content &&
                content.TryGetValue("application/xml", out var mediaType))
            {
                mediaType.Schema = QylXmlSchema.Reference(document, metadata.Shape);
            }
        }

        return Task.CompletedTask;
    }
}
