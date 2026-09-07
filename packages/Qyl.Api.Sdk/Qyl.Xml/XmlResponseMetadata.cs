namespace Qyl.Xml;

/// <summary>
/// Endpoint metadata: the response with <paramref name="StatusCode"/> is an <c>application/xml</c> document of <paramref name="Shape"/>.
/// Added by <see cref="XmlHttpResult{TValue}"/>; the OpenAPI document of a Qyl API describes the response from it.
/// </summary>
/// <param name="StatusCode">The status code of the response.</param>
/// <param name="Shape">The element tree of the document.</param>
public sealed record XmlResponseMetadata(int StatusCode, XmlShape Shape);
