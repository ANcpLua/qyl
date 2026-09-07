namespace Qyl.Xml;

/// <summary>
/// Marks a <see langword="partial"/> class or record whose XML representation is written by generated
/// <see cref="System.Xml.XmlWriter"/> code. The type receives an <see cref="IXmlWritable"/> implementation at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Shape comes from the standard <see cref="System.Xml.Serialization"/> attributes and is written exactly as <c>XmlSerializer</c>
/// would write it: <c>[XmlRoot]</c> names the document element, <c>[XmlAttribute]</c>, <c>[XmlElement]</c>, and <c>[XmlText]</c> map
/// scalars, <c>[XmlIgnore]</c> skips a property, <c>[XmlEnum]</c> renames an enum member. A property whose type is itself a
/// <c>[GenerateXml]</c> model becomes a child element; a collection becomes repeated elements under <c>[XmlElement]</c> or a wrapper
/// element under <c>[XmlArray]</c> and <c>[XmlArrayItem]</c>. Properties of base classes declared in the same compilation are
/// written first, in declaration order. The same tree is exposed as data through <see cref="IXmlWritable.XmlShape"/>, which the
/// OpenAPI contract of a Qyl API describes the <c>application/xml</c> response with.
/// </para>
/// <para>
/// Every option the generator does not implement, and every <c>XmlSerializer</c> convention it cannot honour, such as
/// <c>ShouldSerializeX()</c>, <c>XSpecified</c>, or <c>[DefaultValue]</c>, is a compile error, never a silent difference. Two
/// intentional extensions where <c>XmlSerializer</c> throws instead: read-only properties are written, and a <c>Nullable&lt;T&gt;</c>
/// attribute is omitted when absent.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateXmlAttribute : Attribute;
