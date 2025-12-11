
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class CorrelatedError : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public double? CorrelationStrength { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ErrorId { get; set; }
#nullable restore
#else
        public string ErrorId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ErrorType { get; set; }
#nullable restore
#else
        public string ErrorType { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.TemporalRelationship? TemporalRelationship { get; set; }
                public CorrelatedError()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Error.CorrelatedError CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Error.CorrelatedError();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "correlation_strength", n => { CorrelationStrength = n.GetDoubleValue(); } },
                { "error_id", n => { ErrorId = n.GetStringValue(); } },
                { "error_type", n => { ErrorType = n.GetStringValue(); } },
                { "temporal_relationship", n => { TemporalRelationship = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.TemporalRelationship>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteDoubleValue("correlation_strength", CorrelationStrength);
            writer.WriteStringValue("error_id", ErrorId);
            writer.WriteStringValue("error_type", ErrorType);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.TemporalRelationship>("temporal_relationship", TemporalRelationship);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
