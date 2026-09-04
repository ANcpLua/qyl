namespace Qyl.Xml;

/// <summary>
/// Marks a <see langword="partial"/> class or record whose XML representation is written by generated
/// <see cref="System.Xml.XmlWriter"/> code. The type receives an <see cref="IXmlWritable"/> implementation at compile time.
/// </summary>
/// <remarks>
/// Shape comes from the standard <see cref="System.Xml.Serialization"/> attributes: <c>[XmlRoot]</c> names the document element,
/// <c>[XmlAttribute]</c> and <c>[XmlElement]</c> map properties, <c>[XmlIgnore]</c> skips one. Only the name and namespace of those
/// attributes are honoured; anything else is a compile error, never a silent difference to <c>XmlSerializer</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateXmlAttribute : Attribute;
