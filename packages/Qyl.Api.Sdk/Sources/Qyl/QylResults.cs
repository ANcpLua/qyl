using Qyl.Xml;

namespace Qyl;

/// <summary>Typed results a Qyl API returns in addition to <see cref="TypedResults"/>.</summary>
public static class QylResults
{
    /// <summary>Writes <paramref name="value"/> as a <c>200 application/xml</c> response.</summary>
    /// <typeparam name="TValue">The document type; a <c>[GenerateXml]</c> type in practice.</typeparam>
    /// <param name="value">The document to write.</param>
    /// <returns>The typed result, usable inside <c>Results&lt;...&gt;</c> unions.</returns>
    public static XmlHttpResult<TValue> Xml<TValue>(TValue value)
        where TValue : IXmlWritable
    {
        return new XmlHttpResult<TValue>(value);
    }
}
