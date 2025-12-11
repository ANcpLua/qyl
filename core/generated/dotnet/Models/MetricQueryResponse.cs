
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class MetricQueryResponse : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? MetricName { get; set; }
#nullable restore
#else
        public string MetricName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.MetricTimeSeries>? Series { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.MetricTimeSeries> Series { get; set; }
#endif
                public MetricQueryResponse()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.MetricQueryResponse CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.MetricQueryResponse();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "metric_name", n => { MetricName = n.GetStringValue(); } },
                { "series", n => { Series = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.MetricTimeSeries>(global::Qyl.Core.Models.MetricTimeSeries.CreateFromDiscriminatorValue)?.AsList(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("metric_name", MetricName);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.MetricTimeSeries>("series", Series);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
