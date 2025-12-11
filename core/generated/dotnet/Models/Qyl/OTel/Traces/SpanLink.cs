
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Common;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.OTel.Traces
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class SpanLink : IAdditionalDataHolder, IParsable
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
                public int? Flags { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? SpanId { get; set; }
#nullable restore
#else
        public string SpanId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TraceId { get; set; }
#nullable restore
#else
        public string TraceId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TraceState { get; set; }
#nullable restore
#else
        public string TraceState { get; set; }
#endif
                public SpanLink()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.OTel.Traces.SpanLink CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.OTel.Traces.SpanLink();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "attributes", n => { Attributes = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>(global::Qyl.Core.Models.Qyl.Common.AttributeObject.CreateFromDiscriminatorValue)?.AsList(); } },
                { "dropped_attributes_count", n => { DroppedAttributesCount = n.GetLongValue(); } },
                { "flags", n => { Flags = n.GetIntValue(); } },
                { "span_id", n => { SpanId = n.GetStringValue(); } },
                { "trace_id", n => { TraceId = n.GetStringValue(); } },
                { "trace_state", n => { TraceState = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>("attributes", Attributes);
            writer.WriteLongValue("dropped_attributes_count", DroppedAttributesCount);
            writer.WriteIntValue("flags", Flags);
            writer.WriteStringValue("span_id", SpanId);
            writer.WriteStringValue("trace_id", TraceId);
            writer.WriteStringValue("trace_state", TraceState);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
