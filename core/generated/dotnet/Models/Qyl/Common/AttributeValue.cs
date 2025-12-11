
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Serialization;
namespace Qyl.Core.Models.Qyl.Common
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class AttributeValue : IComposedTypeWrapper, IParsable
    {
                public bool? AttributeValueBoolean { get; set; }
                public double? AttributeValueDouble { get; set; }
                public long? AttributeValueInt64 { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? AttributeValueString { get; set; }
#nullable restore
#else
        public string AttributeValueString { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<bool?>? Boolean { get; set; }
#nullable restore
#else
        public List<bool?> Boolean { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<double?>? Double { get; set; }
#nullable restore
#else
        public List<double?> Double { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<long?>? Int64 { get; set; }
#nullable restore
#else
        public List<long?> Int64 { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<string>? String { get; set; }
#nullable restore
#else
        public List<string> String { get; set; }
#endif
                public static global::Qyl.Core.Models.Qyl.Common.AttributeValue CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            var result = new global::Qyl.Core.Models.Qyl.Common.AttributeValue();
            if(parseNode.GetBoolValue() is bool attributeValueBooleanValue)
            {
                result.AttributeValueBoolean = attributeValueBooleanValue;
            }
            else if(parseNode.GetDoubleValue() is double attributeValueDoubleValue)
            {
                result.AttributeValueDouble = attributeValueDoubleValue;
            }
            else if(parseNode.GetLongValue() is long attributeValueInt64Value)
            {
                result.AttributeValueInt64 = attributeValueInt64Value;
            }
            else if(parseNode.GetStringValue() is string attributeValueStringValue)
            {
                result.AttributeValueString = attributeValueStringValue;
            }
            else if(parseNode.GetCollectionOfPrimitiveValues<bool?>()?.AsList() is List<bool> booleanValue)
            {
                result.Boolean = booleanValue;
            }
            else if(parseNode.GetCollectionOfPrimitiveValues<double?>()?.AsList() is List<double> doubleValue)
            {
                result.Double = doubleValue;
            }
            else if(parseNode.GetCollectionOfPrimitiveValues<long?>()?.AsList() is List<long> int64Value)
            {
                result.Int64 = int64Value;
            }
            else if(parseNode.GetCollectionOfPrimitiveValues<string>()?.AsList() is List<string> stringValue)
            {
                result.String = stringValue;
            }
            return result;
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>();
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            if(AttributeValueBoolean != null)
            {
                writer.WriteBoolValue(null, AttributeValueBoolean);
            }
            else if(AttributeValueDouble != null)
            {
                writer.WriteDoubleValue(null, AttributeValueDouble);
            }
            else if(AttributeValueInt64 != null)
            {
                writer.WriteLongValue(null, AttributeValueInt64);
            }
            else if(AttributeValueString != null)
            {
                writer.WriteStringValue(null, AttributeValueString);
            }
            else if(Boolean != null)
            {
                writer.WriteCollectionOfPrimitiveValues<bool?>(null, Boolean);
            }
            else if(Double != null)
            {
                writer.WriteCollectionOfPrimitiveValues<double?>(null, Double);
            }
            else if(Int64 != null)
            {
                writer.WriteCollectionOfPrimitiveValues<long?>(null, Int64);
            }
            else if(String != null)
            {
                writer.WriteCollectionOfPrimitiveValues<string>(null, String);
            }
        }
    }
}
#pragma warning restore CS0618
