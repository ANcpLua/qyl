
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Domains.Observe.Error;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ErrorUpdate : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? AssignedTo { get; set; }
#nullable restore
#else
        public string AssignedTo { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? IssueUrl { get; set; }
#nullable restore
#else
        public string IssueUrl { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus? Status { get; set; }
                public ErrorUpdate()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.ErrorUpdate CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.ErrorUpdate();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "assigned_to", n => { AssignedTo = n.GetStringValue(); } },
                { "issue_url", n => { IssueUrl = n.GetStringValue(); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("assigned_to", AssignedTo);
            writer.WriteStringValue("issue_url", IssueUrl);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus>("status", Status);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
