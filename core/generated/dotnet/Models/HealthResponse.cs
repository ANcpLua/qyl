
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class HealthResponse : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.HealthResponse_components? Components { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.HealthResponse_components Components { get; set; }
#endif
                public global::Qyl.Core.Models.HealthStatus? Status { get; set; }
                public long? UptimeSeconds { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Version { get; set; }
#nullable restore
#else
        public string Version { get; set; }
#endif
                public HealthResponse()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.HealthResponse CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.HealthResponse();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "components", n => { Components = n.GetObjectValue<global::Qyl.Core.Models.HealthResponse_components>(global::Qyl.Core.Models.HealthResponse_components.CreateFromDiscriminatorValue); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.HealthStatus>(); } },
                { "uptime_seconds", n => { UptimeSeconds = n.GetLongValue(); } },
                { "version", n => { Version = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteObjectValue<global::Qyl.Core.Models.HealthResponse_components>("components", Components);
            writer.WriteEnumValue<global::Qyl.Core.Models.HealthStatus>("status", Status);
            writer.WriteLongValue("uptime_seconds", UptimeSeconds);
            writer.WriteStringValue("version", Version);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
