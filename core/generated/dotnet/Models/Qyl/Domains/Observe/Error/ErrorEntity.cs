
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.Domains.Observe.Error
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class ErrorEntity : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<string>? AffectedServices { get; set; }
#nullable restore
#else
        public List<string> AffectedServices { get; set; }
#endif
                public long? AffectedUsers { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? AssignedTo { get; set; }
#nullable restore
#else
        public string AssignedTo { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategory? Category { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ErrorId { get; set; }
#nullable restore
#else
        public string ErrorId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ErrorType { get; set; }
#nullable restore
#else
        public string ErrorType { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Fingerprint { get; set; }
#nullable restore
#else
        public string Fingerprint { get; set; }
#endif
                public DateTimeOffset? FirstSeen { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? IssueUrl { get; set; }
#nullable restore
#else
        public string IssueUrl { get; set; }
#endif
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
        public List<string>? SampleTraces { get; set; }
#nullable restore
#else
        public List<string> SampleTraces { get; set; }
#endif
                public global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus? Status { get; set; }
                public ErrorEntity()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorEntity CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorEntity();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "affected_services", n => { AffectedServices = n.GetCollectionOfPrimitiveValues<string>()?.AsList(); } },
                { "affected_users", n => { AffectedUsers = n.GetLongValue(); } },
                { "assigned_to", n => { AssignedTo = n.GetStringValue(); } },
                { "category", n => { Category = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategory>(); } },
                { "error_id", n => { ErrorId = n.GetStringValue(); } },
                { "error.type", n => { ErrorType = n.GetStringValue(); } },
                { "fingerprint", n => { Fingerprint = n.GetStringValue(); } },
                { "first_seen", n => { FirstSeen = n.GetDateTimeOffsetValue(); } },
                { "issue_url", n => { IssueUrl = n.GetStringValue(); } },
                { "last_seen", n => { LastSeen = n.GetDateTimeOffsetValue(); } },
                { "message", n => { Message = n.GetStringValue(); } },
                { "occurrence_count", n => { OccurrenceCount = n.GetLongValue(); } },
                { "sample_traces", n => { SampleTraces = n.GetCollectionOfPrimitiveValues<string>()?.AsList(); } },
                { "status", n => { Status = n.GetEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus>(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfPrimitiveValues<string>("affected_services", AffectedServices);
            writer.WriteLongValue("affected_users", AffectedUsers);
            writer.WriteStringValue("assigned_to", AssignedTo);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorCategory>("category", Category);
            writer.WriteStringValue("error_id", ErrorId);
            writer.WriteStringValue("error.type", ErrorType);
            writer.WriteStringValue("fingerprint", Fingerprint);
            writer.WriteDateTimeOffsetValue("first_seen", FirstSeen);
            writer.WriteStringValue("issue_url", IssueUrl);
            writer.WriteDateTimeOffsetValue("last_seen", LastSeen);
            writer.WriteStringValue("message", Message);
            writer.WriteLongValue("occurrence_count", OccurrenceCount);
            writer.WriteCollectionOfPrimitiveValues<string>("sample_traces", SampleTraces);
            writer.WriteEnumValue<global::Qyl.Core.Models.Qyl.Domains.Observe.Error.ErrorStatus>("status", Status);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
