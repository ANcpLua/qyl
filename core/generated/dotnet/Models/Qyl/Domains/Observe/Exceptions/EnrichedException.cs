
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Common;
using Qyl.Core.Models.Qyl.Domains.AI.Code;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Exceptions
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class EnrichedException : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public long? AffectedUsers { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.EnrichedException? Cause { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.EnrichedException Cause { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject>? Data { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject> Data { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ExceptionType { get; set; }
#nullable restore
#else
        public string ExceptionType { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Fingerprint { get; set; }
#nullable restore
#else
        public string Fingerprint { get; set; }
#endif
                public DateTimeOffset? FirstSeen { get; set; }
                public DateTimeOffset? LastSeen { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Message { get; set; }
#nullable restore
#else
        public string Message { get; set; }
#endif
                public long? OccurrenceCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackTrace? StackTrace { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackTrace StackTrace { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionStatus? Status { get; set; }
                public EnrichedException()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.EnrichedException CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.EnrichedException();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "affected_users", n => { AffectedUsers = n.GetLongValue(); } },
                { "cause", n => { Cause = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.EnrichedException>(global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.EnrichedException.CreateFromDiscriminatorValue); } },
                { "data", n => { Data = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>(global::Qyl.Core.Models.Qyl.Common.AttributeObject.CreateFromDiscriminatorValue)?.AsList(); } },
                { "exception_type", n => { ExceptionType = n.GetStringValue(); } },
                { "fingerprint", n => { Fingerprint = n.GetStringValue(); } },
                { "first_seen", n => { FirstSeen = n.GetDateTimeOffsetValue(); } },
                { "last_seen", n => { LastSeen = n.GetDateTimeOffsetValue(); } },
                { "message", n => { Message = n.GetStringValue(); } },
                { "occurrence_count", n => { OccurrenceCount = n.GetLongValue(); } },
                { "stack_trace", n => { StackTrace = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackTrace>(global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackTrace.CreateFromDiscriminatorValue); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionStatus>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteLongValue("affected_users", AffectedUsers);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.EnrichedException>("cause", Cause);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>("data", Data);
            writer.WriteStringValue("exception_type", ExceptionType);
            writer.WriteStringValue("fingerprint", Fingerprint);
            writer.WriteDateTimeOffsetValue("first_seen", FirstSeen);
            writer.WriteDateTimeOffsetValue("last_seen", LastSeen);
            writer.WriteStringValue("message", Message);
            writer.WriteLongValue("occurrence_count", OccurrenceCount);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Domains.AI.Code.StackTrace>("stack_trace", StackTrace);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Exceptions.ExceptionStatus>("status", Status);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
