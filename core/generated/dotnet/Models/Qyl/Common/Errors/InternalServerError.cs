
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Common.Errors
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class InternalServerError : ApiException, IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Detail { get; set; }
#nullable restore
#else
        public string Detail { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ErrorCode { get; set; }
#nullable restore
#else
        public string ErrorCode { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Instance { get; set; }
#nullable restore
#else
        public string Instance { get; set; }
#endif
                public override string Message { get => base.Message; }
                public int? Status { get; set; }
                public DateTimeOffset? Timestamp { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Type { get; set; }
#nullable restore
#else
        public string Type { get; set; }
#endif
                public InternalServerError()
        {
            AdditionalData = new Dictionary<string, object>();
            Type = "about:blank";
        }
                public static global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Common.Errors.InternalServerError();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "detail", n => { Detail = n.GetStringValue(); } },
                { "error_code", n => { ErrorCode = n.GetStringValue(); } },
                { "instance", n => { Instance = n.GetStringValue(); } },
                { "status", n => { Status = n.GetIntValue(); } },
                { "timestamp", n => { Timestamp = n.GetDateTimeOffsetValue(); } },
                { "type", n => { Type = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("detail", Detail);
            writer.WriteStringValue("error_code", ErrorCode);
            writer.WriteStringValue("instance", Instance);
            writer.WriteIntValue("status", Status);
            writer.WriteDateTimeOffsetValue("timestamp", Timestamp);
            writer.WriteStringValue("type", Type);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
