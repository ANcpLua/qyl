
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Ops.Cicd
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class PipelineRunEvent : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? CicdPipelineName { get; set; }
#nullable restore
#else
        public string CicdPipelineName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? CicdPipelineRunId { get; set; }
#nullable restore
#else
        public string CicdPipelineRunId { get; set; }
#endif
                public double? DurationS { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdEventName? EventName { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdPipelineStatus? Status { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdSystem? System { get; set; }
                public DateTimeOffset? Timestamp { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdTriggerType? TriggerType { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? VcsRepositoryRefName { get; set; }
#nullable restore
#else
        public string VcsRepositoryRefName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? VcsRepositoryRefRevision { get; set; }
#nullable restore
#else
        public string VcsRepositoryRefRevision { get; set; }
#endif
                public PipelineRunEvent()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.PipelineRunEvent CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.PipelineRunEvent();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "cicd.pipeline.name", n => { CicdPipelineName = n.GetStringValue(); } },
                { "cicd.pipeline.run.id", n => { CicdPipelineRunId = n.GetStringValue(); } },
                { "duration_s", n => { DurationS = n.GetDoubleValue(); } },
                { "event.name", n => { EventName = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdEventName>(); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdPipelineStatus>(); } },
                { "system", n => { System = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdSystem>(); } },
                { "timestamp", n => { Timestamp = n.GetDateTimeOffsetValue(); } },
                { "trigger_type", n => { TriggerType = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdTriggerType>(); } },
                { "vcs.repository.ref.name", n => { VcsRepositoryRefName = n.GetStringValue(); } },
                { "vcs.repository.ref.revision", n => { VcsRepositoryRefRevision = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("cicd.pipeline.name", CicdPipelineName);
            writer.WriteStringValue("cicd.pipeline.run.id", CicdPipelineRunId);
            writer.WriteDoubleValue("duration_s", DurationS);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdEventName>("event.name", EventName);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdPipelineStatus>("status", Status);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdSystem>("system", System);
            writer.WriteDateTimeOffsetValue("timestamp", Timestamp);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Ops.Cicd.CicdTriggerType>("trigger_type", TriggerType);
            writer.WriteStringValue("vcs.repository.ref.name", VcsRepositoryRefName);
            writer.WriteStringValue("vcs.repository.ref.revision", VcsRepositoryRefRevision);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
