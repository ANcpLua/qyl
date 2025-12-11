
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class MetricTimeSeries : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.MetricTimeSeries_labels? Labels { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.MetricTimeSeries_labels Labels { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.MetricDataPoint>? Points { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.MetricDataPoint> Points { get; set; }
#endif
                public MetricTimeSeries()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.MetricTimeSeries CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.MetricTimeSeries();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "labels", n => { Labels = n.GetObjectValue<global::Qyl.Core.Models.MetricTimeSeries_labels>(global::Qyl.Core.Models.MetricTimeSeries_labels.CreateFromDiscriminatorValue); } },
                { "points", n => { Points = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.MetricDataPoint>(global::Qyl.Core.Models.MetricDataPoint.CreateFromDiscriminatorValue)?.AsList(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteObjectValue<global::Qyl.Core.Models.MetricTimeSeries_labels>("labels", Labels);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.MetricDataPoint>("points", Points);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
