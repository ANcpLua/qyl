using System.Xml.Serialization;

namespace Qyl.Sample;

/// <summary>A todo item. JSON through <see cref="AppJsonSerializerContext"/>; XML through the generated <see cref="IXmlWritable"/> implementation.</summary>
/// <param name="Id">Identity of the todo.</param>
/// <param name="Title">What has to be done.</param>
/// <param name="DueBy">The day it is due, if any.</param>
/// <param name="IsComplete">Whether it is done.</param>
[GenerateXml]
[XmlRoot("todo")]
internal sealed partial record Todo(
    [property: XmlAttribute("id")] int Id,
    [property: XmlElement("title")] string? Title,
    [property: XmlElement("due-by")] DateOnly? DueBy = null,
    [property: XmlElement("is-complete")] bool IsComplete = false);
