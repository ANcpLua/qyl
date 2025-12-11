
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Session
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class SessionStats : IAdditionalDataHolder, IParsable
    {
                public long? ActiveSessions { get; set; }
                public IDictionary<string, object> AdditionalData { get; set; }
                public double? AvgDurationMs { get; set; }
                public double? BounceRate { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionCountryStats>? ByCountry { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionCountryStats> ByCountry { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionDeviceStats>? ByDeviceType { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionDeviceStats> ByDeviceType { get; set; }
#endif
                public long? SessionsWithErrors { get; set; }
                public long? SessionsWithGenai { get; set; }
                public long? TotalSessions { get; set; }
                public long? UniqueUsers { get; set; }
                public SessionStats()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionStats CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionStats();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "active_sessions", n => { ActiveSessions = n.GetLongValue(); } },
                { "avg_duration_ms", n => { AvgDurationMs = n.GetDoubleValue(); } },
                { "bounce_rate", n => { BounceRate = n.GetDoubleValue(); } },
                { "by_country", n => { ByCountry = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionCountryStats>(global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionCountryStats.CreateFromDiscriminatorValue)?.AsList(); } },
                { "by_device_type", n => { ByDeviceType = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionDeviceStats>(global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionDeviceStats.CreateFromDiscriminatorValue)?.AsList(); } },
                { "sessions_with_errors", n => { SessionsWithErrors = n.GetLongValue(); } },
                { "sessions_with_genai", n => { SessionsWithGenai = n.GetLongValue(); } },
                { "total_sessions", n => { TotalSessions = n.GetLongValue(); } },
                { "unique_users", n => { UniqueUsers = n.GetLongValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteLongValue("active_sessions", ActiveSessions);
            writer.WriteDoubleValue("avg_duration_ms", AvgDurationMs);
            writer.WriteDoubleValue("bounce_rate", BounceRate);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionCountryStats>("by_country", ByCountry);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionDeviceStats>("by_device_type", ByDeviceType);
            writer.WriteLongValue("sessions_with_errors", SessionsWithErrors);
            writer.WriteLongValue("sessions_with_genai", SessionsWithGenai);
            writer.WriteLongValue("total_sessions", TotalSessions);
            writer.WriteLongValue("unique_users", UniqueUsers);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
