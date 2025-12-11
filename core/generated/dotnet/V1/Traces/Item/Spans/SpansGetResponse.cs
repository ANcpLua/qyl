
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.OTel.Traces;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.V1.Traces.Item.Spans
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class SpansGetResponse : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public bool? HasMore { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.OTel.Traces.Span>? Items { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.OTel.Traces.Span> Items { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? NextCursor { get; set; }
#nullable restore
#else
        public string NextCursor { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? PrevCursor { get; set; }
#nullable restore
#else
        public string PrevCursor { get; set; }
#endif
                public SpansGetResponse()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.V1.Traces.Item.Spans.SpansGetResponse CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.V1.Traces.Item.Spans.SpansGetResponse();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "has_more", n => { HasMore = n.GetBoolValue(); } },
                { "items", n => { Items = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.OTel.Traces.Span>(global::Qyl.Core.Models.Qyl.OTel.Traces.Span.CreateFromDiscriminatorValue)?.AsList(); } },
                { "next_cursor", n => { NextCursor = n.GetStringValue(); } },
                { "prev_cursor", n => { PrevCursor = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteBoolValue("has_more", HasMore);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.OTel.Traces.Span>("items", Items);
            writer.WriteStringValue("next_cursor", NextCursor);
            writer.WriteStringValue("prev_cursor", PrevCursor);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
