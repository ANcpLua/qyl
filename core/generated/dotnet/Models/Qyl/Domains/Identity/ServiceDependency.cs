
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Identity
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ServiceDependency : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public double? AvgLatencyMs { get; set; }
                public double? ErrorRate { get; set; }
                public long? RequestCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? SourceService { get; set; }
#nullable restore
#else
        public string SourceService { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TargetService { get; set; }
#nullable restore
#else
        public string TargetService { get; set; }
#endif
                public ServiceDependency()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Identity.ServiceDependency CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Identity.ServiceDependency();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "avg_latency_ms", n => { AvgLatencyMs = n.GetDoubleValue(); } },
                { "error_rate", n => { ErrorRate = n.GetDoubleValue(); } },
                { "request_count", n => { RequestCount = n.GetLongValue(); } },
                { "source_service", n => { SourceService = n.GetStringValue(); } },
                { "target_service", n => { TargetService = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteDoubleValue("avg_latency_ms", AvgLatencyMs);
            writer.WriteDoubleValue("error_rate", ErrorRate);
            writer.WriteLongValue("request_count", RequestCount);
            writer.WriteStringValue("source_service", SourceService);
            writer.WriteStringValue("target_service", TargetService);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
