
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Domains.Ops.Cicd;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class PipelineStatusStats : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public long? Count { get; set; }
                public double? Percentage { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdPipelineStatus? Status { get; set; }
                public PipelineStatusStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.PipelineStatusStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.PipelineStatusStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "count", n => { Count = n.GetLongValue(); } },
                { "percentage", n => { Percentage = n.GetDoubleValue(); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdPipelineStatus>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteLongValue("count", Count);
            writer.WriteDoubleValue("percentage", Percentage);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdPipelineStatus>("status", Status);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
