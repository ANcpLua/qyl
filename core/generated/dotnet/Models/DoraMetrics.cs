
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class DoraMetrics : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public double? ChangeFailureRate { get; set; }
                public double? DeploymentFrequency { get; set; }
                public double? LeadTimeHours { get; set; }
                public double? MttrHours { get; set; }
                public global::Qyl.Core.Models.DoraPerformanceLevel? PerformanceLevel { get; set; }
                public DoraMetrics()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.DoraMetrics CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.DoraMetrics();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "change_failure_rate", n => { ChangeFailureRate = n.GetDoubleValue(); } },
                { "deployment_frequency", n => { DeploymentFrequency = n.GetDoubleValue(); } },
                { "lead_time_hours", n => { LeadTimeHours = n.GetDoubleValue(); } },
                { "mttr_hours", n => { MttrHours = n.GetDoubleValue(); } },
                { "performance_level", n => { PerformanceLevel = n.GetEnumValue<global::Qyl.Core.Models.DoraPerformanceLevel>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteDoubleValue("change_failure_rate", ChangeFailureRate);
            writer.WriteDoubleValue("deployment_frequency", DeploymentFrequency);
            writer.WriteDoubleValue("lead_time_hours", LeadTimeHours);
            writer.WriteDoubleValue("mttr_hours", MttrHours);
            writer.WriteEnumValue<global::Qyl.Core.Models.DoraPerformanceLevel>("performance_level", PerformanceLevel);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
