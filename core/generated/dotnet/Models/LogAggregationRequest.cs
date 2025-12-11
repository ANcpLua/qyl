
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Domains.Observe.Log;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class LogAggregationRequest : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogAggregation? Aggregation { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogAggregation Aggregation { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogQuery? Query { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogQuery Query { get; set; }
#endif
                public LogAggregationRequest()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.LogAggregationRequest CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.LogAggregationRequest();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "aggregation", n => { Aggregation = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogAggregation>(global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogAggregation.CreateFromDiscriminatorValue); } },
                { "query", n => { Query = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogQuery>(global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogQuery.CreateFromDiscriminatorValue); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogAggregation>("aggregation", Aggregation);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Log.LogQuery>("query", Query);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
