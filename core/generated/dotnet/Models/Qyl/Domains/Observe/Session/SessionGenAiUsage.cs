
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Session
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class SessionGenAiUsage : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public double? EstimatedCostUsd { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<string>? ModelsUsed { get; set; }
#nullable restore
#else
        public List<string> ModelsUsed { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<string>? ProvidersUsed { get; set; }
#nullable restore
#else
        public List<string> ProvidersUsed { get; set; }
#endif
                public int? RequestCount { get; set; }
                public long? TotalInputTokens { get; set; }
                public long? TotalOutputTokens { get; set; }
                public SessionGenAiUsage()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGenAiUsage CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGenAiUsage();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "estimated_cost_usd", n => { EstimatedCostUsd = n.GetDoubleValue(); } },
                { "models_used", n => { ModelsUsed = n.GetCollectionOfPrimitiveValues<string>()?.AsList(); } },
                { "providers_used", n => { ProvidersUsed = n.GetCollectionOfPrimitiveValues<string>()?.AsList(); } },
                { "request_count", n => { RequestCount = n.GetIntValue(); } },
                { "total_input_tokens", n => { TotalInputTokens = n.GetLongValue(); } },
                { "total_output_tokens", n => { TotalOutputTokens = n.GetLongValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteDoubleValue("estimated_cost_usd", EstimatedCostUsd);
            writer.WriteCollectionOfPrimitiveValues<string>("models_used", ModelsUsed);
            writer.WriteCollectionOfPrimitiveValues<string>("providers_used", ProvidersUsed);
            writer.WriteIntValue("request_count", RequestCount);
            writer.WriteLongValue("total_input_tokens", TotalInputTokens);
            writer.WriteLongValue("total_output_tokens", TotalOutputTokens);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
