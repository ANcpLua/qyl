
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class PipelineStats : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public double? AvgDurationSeconds { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.PipelineStatusStats>? ByStatus { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.PipelineStatusStats> ByStatus { get; set; }
#endif
                public double? P95DurationSeconds { get; set; }
                public double? SuccessRate { get; set; }
                public long? TotalRuns { get; set; }
                public PipelineStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.PipelineStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.PipelineStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "avg_duration_seconds", n => { AvgDurationSeconds = n.GetDoubleValue(); } },
                { "by_status", n => { ByStatus = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.PipelineStatusStats>(global::Qyl.Core.Models.PipelineStatusStats.CreateFromDiscriminatorValue)?.AsList(); } },
                { "p95_duration_seconds", n => { P95DurationSeconds = n.GetDoubleValue(); } },
                { "success_rate", n => { SuccessRate = n.GetDoubleValue(); } },
                { "total_runs", n => { TotalRuns = n.GetLongValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteDoubleValue("avg_duration_seconds", AvgDurationSeconds);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.PipelineStatusStats>("by_status", ByStatus);
            writer.WriteDoubleValue("p95_duration_seconds", P95DurationSeconds);
            writer.WriteDoubleValue("success_rate", SuccessRate);
            writer.WriteLongValue("total_runs", TotalRuns);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
