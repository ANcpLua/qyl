
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Session
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class SessionEntity : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionClientInfo? Client { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionClientInfo Client { get; set; }
#endif
                public double? DurationMs { get; set; }
                public DateTimeOffset? EndTime { get; set; }
                public int? ErrorCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGenAiUsage? GenaiUsage { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGenAiUsage GenaiUsage { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGeoInfo? Geo { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGeoInfo Geo { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? SessionId { get; set; }
#nullable restore
#else
        public string SessionId { get; set; }
#endif
                public int? SpanCount { get; set; }
                public DateTimeOffset? StartTime { get; set; }
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionState? State { get; set; }
                public int? TraceCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? UserId { get; set; }
#nullable restore
#else
        public string UserId { get; set; }
#endif
                public SessionEntity()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionEntity CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionEntity();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "client", n => { Client = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionClientInfo>(global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionClientInfo.CreateFromDiscriminatorValue); } },
                { "duration_ms", n => { DurationMs = n.GetDoubleValue(); } },
                { "end_time", n => { EndTime = n.GetDateTimeOffsetValue(); } },
                { "error_count", n => { ErrorCount = n.GetIntValue(); } },
                { "genai_usage", n => { GenaiUsage = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGenAiUsage>(global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGenAiUsage.CreateFromDiscriminatorValue); } },
                { "geo", n => { Geo = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGeoInfo>(global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGeoInfo.CreateFromDiscriminatorValue); } },
                { "session.id", n => { SessionId = n.GetStringValue(); } },
                { "span_count", n => { SpanCount = n.GetIntValue(); } },
                { "start_time", n => { StartTime = n.GetDateTimeOffsetValue(); } },
                { "state", n => { State = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionState>(); } },
                { "trace_count", n => { TraceCount = n.GetIntValue(); } },
                { "user.id", n => { UserId = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionClientInfo>("client", Client);
            writer.WriteDoubleValue("duration_ms", DurationMs);
            writer.WriteDateTimeOffsetValue("end_time", EndTime);
            writer.WriteIntValue("error_count", ErrorCount);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGenAiUsage>("genai_usage", GenaiUsage);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionGeoInfo>("geo", Geo);
            writer.WriteStringValue("session.id", SessionId);
            writer.WriteIntValue("span_count", SpanCount);
            writer.WriteDateTimeOffsetValue("start_time", StartTime);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Session.SessionState>("state", State);
            writer.WriteIntValue("trace_count", TraceCount);
            writer.WriteStringValue("user.id", UserId);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
