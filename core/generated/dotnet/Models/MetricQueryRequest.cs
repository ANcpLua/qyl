
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Common.Pagination;
using Qyl.Core.Models.Qyl.OTel.Metrics;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class MetricQueryRequest : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public global::Qyl.Core.Models.Qyl.OTel.Metrics.AggregationFunction? Aggregation { get; set; }
                public DateTimeOffset? EndTime { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.MetricQueryRequest_filters? Filters { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.MetricQueryRequest_filters Filters { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<string>? GroupBy { get; set; }
#nullable restore
#else
        public List<string> GroupBy { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? MetricName { get; set; }
#nullable restore
#else
        public string MetricName { get; set; }
#endif
                public DateTimeOffset? StartTime { get; set; }
                public global::Qyl.Core.Models.Qyl.Common.Pagination.TimeBucket? Step { get; set; }
                public MetricQueryRequest()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.MetricQueryRequest CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.MetricQueryRequest();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "aggregation", n => { Aggregation = n.GetEnumValue<global::Qyl.Core.Models.Qyl.OTel.Metrics.AggregationFunction>(); } },
                { "end_time", n => { EndTime = n.GetDateTimeOffsetValue(); } },
                { "filters", n => { Filters = n.GetObjectValue<global::Qyl.Core.Models.MetricQueryRequest_filters>(global::Qyl.Core.Models.MetricQueryRequest_filters.CreateFromDiscriminatorValue); } },
                { "group_by", n => { GroupBy = n.GetCollectionOfPrimitiveValues<string>()?.AsList(); } },
                { "metric_name", n => { MetricName = n.GetStringValue(); } },
                { "start_time", n => { StartTime = n.GetDateTimeOffsetValue(); } },
                { "step", n => { Step = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Common.Pagination.TimeBucket>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.OTel.Metrics.AggregationFunction>("aggregation", Aggregation);
            writer.WriteDateTimeOffsetValue("end_time", EndTime);
            writer.WriteObjectValue<global::Qyl.Core.Models.MetricQueryRequest_filters>("filters", Filters);
            writer.WriteCollectionOfPrimitiveValues<string>("group_by", GroupBy);
            writer.WriteStringValue("metric_name", MetricName);
            writer.WriteDateTimeOffsetValue("start_time", StartTime);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Common.Pagination.TimeBucket>("step", Step);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
