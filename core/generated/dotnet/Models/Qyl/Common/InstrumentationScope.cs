
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Common
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class InstrumentationScope : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject>? Attributes { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject> Attributes { get; set; }
#endif
                public long? DroppedAttributesCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Name { get; set; }
#nullable restore
#else
        public string Name { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Version { get; set; }
#nullable restore
#else
        public string Version { get; set; }
#endif
                public InstrumentationScope()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Common.InstrumentationScope CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Common.InstrumentationScope();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "attributes", n => { Attributes = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>(global::Qyl.Core.Models.Qyl.Common.AttributeObject.CreateFromDiscriminatorValue)?.AsList(); } },
                { "dropped_attributes_count", n => { DroppedAttributesCount = n.GetLongValue(); } },
                { "name", n => { Name = n.GetStringValue(); } },
                { "version", n => { Version = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>("attributes", Attributes);
            writer.WriteLongValue("dropped_attributes_count", DroppedAttributesCount);
            writer.WriteStringValue("name", Name);
            writer.WriteStringValue("version", Version);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
