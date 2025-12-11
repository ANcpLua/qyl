
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class OperationInfo : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public double? AvgDurationMs { get; set; }
                public long? ErrorCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Name { get; set; }
#nullable restore
#else
        public string Name { get; set; }
#endif
                public double? P99DurationMs { get; set; }
                public long? RequestCount { get; set; }
                public double? SpanKind { get; set; }
                public OperationInfo()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.OperationInfo CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.OperationInfo();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "avg_duration_ms", n => { AvgDurationMs = n.GetDoubleValue(); } },
                { "error_count", n => { ErrorCount = n.GetLongValue(); } },
                { "name", n => { Name = n.GetStringValue(); } },
                { "p99_duration_ms", n => { P99DurationMs = n.GetDoubleValue(); } },
                { "request_count", n => { RequestCount = n.GetLongValue(); } },
                { "span_kind", n => { SpanKind = n.GetDoubleValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteDoubleValue("avg_duration_ms", AvgDurationMs);
            writer.WriteLongValue("error_count", ErrorCount);
            writer.WriteStringValue("name", Name);
            writer.WriteDoubleValue("p99_duration_ms", P99DurationMs);
            writer.WriteLongValue("request_count", RequestCount);
            writer.WriteDoubleValue("span_kind", SpanKind);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
