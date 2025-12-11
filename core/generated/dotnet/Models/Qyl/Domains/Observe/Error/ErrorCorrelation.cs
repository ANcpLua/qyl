
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Common;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ErrorCorrelation : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject>? CommonAttributes { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject> CommonAttributes { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.CorrelatedError>? CorrelatedErrors { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.CorrelatedError> CorrelatedErrors { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ErrorId { get; set; }
#nullable restore
#else
        public string ErrorId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? RootCause { get; set; }
#nullable restore
#else
        public string RootCause { get; set; }
#endif
                public ErrorCorrelation()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCorrelation CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCorrelation();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "common_attributes", n => { CommonAttributes = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>(global::Qyl.Core.Models.Qyl.Common.AttributeObject.CreateFromDiscriminatorValue)?.AsList(); } },
                { "correlated_errors", n => { CorrelatedErrors = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.CorrelatedError>(global::Qyl.Core.Models.Qyl.Domains.Observe.Error.CorrelatedError.CreateFromDiscriminatorValue)?.AsList(); } },
                { "error_id", n => { ErrorId = n.GetStringValue(); } },
                { "root_cause", n => { RootCause = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>("common_attributes", CommonAttributes);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.CorrelatedError>("correlated_errors", CorrelatedErrors);
            writer.WriteStringValue("error_id", ErrorId);
            writer.WriteStringValue("root_cause", RootCause);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
