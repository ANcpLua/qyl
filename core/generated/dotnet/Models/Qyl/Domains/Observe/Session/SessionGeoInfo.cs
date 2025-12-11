
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Session
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class SessionGeoInfo : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? City { get; set; }
#nullable restore
#else
        public string City { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? CountryCode { get; set; }
#nullable restore
#else
        public string CountryCode { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? CountryName { get; set; }
#nullable restore
#else
        public string CountryName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? PostalCode { get; set; }
#nullable restore
#else
        public string PostalCode { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Region { get; set; }
#nullable restore
#else
        public string Region { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Timezone { get; set; }
#nullable restore
#else
        public string Timezone { get; set; }
#endif
                public SessionGeoInfo()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGeoInfo CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGeoInfo();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "city", n => { City = n.GetStringValue(); } },
                { "country_code", n => { CountryCode = n.GetStringValue(); } },
                { "country_name", n => { CountryName = n.GetStringValue(); } },
                { "postal_code", n => { PostalCode = n.GetStringValue(); } },
                { "region", n => { Region = n.GetStringValue(); } },
                { "timezone", n => { Timezone = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("city", City);
            writer.WriteStringValue("country_code", CountryCode);
            writer.WriteStringValue("country_name", CountryName);
            writer.WriteStringValue("postal_code", PostalCode);
            writer.WriteStringValue("region", Region);
            writer.WriteStringValue("timezone", Timezone);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
