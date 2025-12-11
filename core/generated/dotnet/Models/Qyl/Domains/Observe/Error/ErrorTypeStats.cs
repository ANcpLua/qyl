
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ErrorTypeStats : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public long? AffectedUsers { get; set; }
                public long? Count { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ErrorType { get; set; }
#nullable restore
#else
        public string ErrorType { get; set; }
#endif
                public double? Percentage { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus? Status { get; set; }
                public ErrorTypeStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTypeStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorTypeStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "affected_users", n => { AffectedUsers = n.GetLongValue(); } },
                { "count", n => { Count = n.GetLongValue(); } },
                { "error_type", n => { ErrorType = n.GetStringValue(); } },
                { "percentage", n => { Percentage = n.GetDoubleValue(); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteLongValue("affected_users", AffectedUsers);
            writer.WriteLongValue("count", Count);
            writer.WriteStringValue("error_type", ErrorType);
            writer.WriteDoubleValue("percentage", Percentage);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus>("status", Status);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
