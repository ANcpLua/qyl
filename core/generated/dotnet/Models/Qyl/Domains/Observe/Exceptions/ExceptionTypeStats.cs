
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Exceptions
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ExceptionTypeStats : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public long? Count { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ExceptionType { get; set; }
#nullable restore
#else
        public string ExceptionType { get; set; }
#endif
                public double? Percentage { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionStatus? Status { get; set; }
                public ExceptionTypeStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTypeStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionTypeStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "count", n => { Count = n.GetLongValue(); } },
                { "exception_type", n => { ExceptionType = n.GetStringValue(); } },
                { "percentage", n => { Percentage = n.GetDoubleValue(); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionStatus>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteLongValue("count", Count);
            writer.WriteStringValue("exception_type", ExceptionType);
            writer.WriteDoubleValue("percentage", Percentage);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionStatus>("status", Status);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
