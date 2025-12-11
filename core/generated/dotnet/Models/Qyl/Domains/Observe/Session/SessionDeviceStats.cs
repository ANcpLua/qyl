
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Session
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class SessionDeviceStats : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public long? Count { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.DeviceType? DeviceType { get; set; }
                public double? Percentage { get; set; }
                public SessionDeviceStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionDeviceStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionDeviceStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "count", n => { Count = n.GetLongValue(); } },
                { "device_type", n => { DeviceType = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.DeviceType>(); } },
                { "percentage", n => { Percentage = n.GetDoubleValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteLongValue("count", Count);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.DeviceType>("device_type", DeviceType);
            writer.WriteDoubleValue("percentage", Percentage);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
