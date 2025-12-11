
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ErrorCategoryStats : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategory? Category { get; set; }
                public long? Count { get; set; }
                public double? Percentage { get; set; }
                public ErrorCategoryStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategoryStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategoryStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "category", n => { Category = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategory>(); } },
                { "count", n => { Count = n.GetLongValue(); } },
                { "percentage", n => { Percentage = n.GetDoubleValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategory>("category", Category);
            writer.WriteLongValue("count", Count);
            writer.WriteDoubleValue("percentage", Percentage);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
