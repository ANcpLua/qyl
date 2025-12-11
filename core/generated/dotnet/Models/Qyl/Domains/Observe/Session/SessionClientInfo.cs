
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Session
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class SessionClientInfo : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Browser { get; set; }
#nullable restore
#else
        public string Browser { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? BrowserVersion { get; set; }
#nullable restore
#else
        public string BrowserVersion { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.DeviceType? DeviceType { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Ip { get; set; }
#nullable restore
#else
        public string Ip { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Os { get; set; }
#nullable restore
#else
        public string Os { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? UserAgent { get; set; }
#nullable restore
#else
        public string UserAgent { get; set; }
#endif
                public SessionClientInfo()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionClientInfo CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionClientInfo();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "browser", n => { Browser = n.GetStringValue(); } },
                { "browser_version", n => { BrowserVersion = n.GetStringValue(); } },
                { "device_type", n => { DeviceType = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.DeviceType>(); } },
                { "ip", n => { Ip = n.GetStringValue(); } },
                { "os", n => { Os = n.GetStringValue(); } },
                { "user_agent", n => { UserAgent = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("browser", Browser);
            writer.WriteStringValue("browser_version", BrowserVersion);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.DeviceType>("device_type", DeviceType);
            writer.WriteStringValue("ip", Ip);
            writer.WriteStringValue("os", Os);
            writer.WriteStringValue("user_agent", UserAgent);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
