
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class TraceQuery : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Cursor { get; set; }
#nullable restore
#else
        public string Cursor { get; set; }
#endif
                public DateTimeOffset? EndTime { get; set; }
                public int? Limit { get; set; }
                public long? MaxDurationMs { get; set; }
                public long? MinDurationMs { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? OperationName { get; set; }
#nullable restore
#else
        public string OperationName { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Query { get; set; }
#nullable restore
#else
        public string Query { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ServiceName { get; set; }
#nullable restore
#else
        public string ServiceName { get; set; }
#endif
                public DateTimeOffset? StartTime { get; set; }
                public double? Status { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.TraceQuery_tags? Tags { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.TraceQuery_tags Tags { get; set; }
#endif
                public TraceQuery()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.TraceQuery CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.TraceQuery();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "cursor", n => { Cursor = n.GetStringValue(); } },
                { "end_time", n => { EndTime = n.GetDateTimeOffsetValue(); } },
                { "limit", n => { Limit = n.GetIntValue(); } },
                { "max_duration_ms", n => { MaxDurationMs = n.GetLongValue(); } },
                { "min_duration_ms", n => { MinDurationMs = n.GetLongValue(); } },
                { "operation_name", n => { OperationName = n.GetStringValue(); } },
                { "query", n => { Query = n.GetStringValue(); } },
                { "service_name", n => { ServiceName = n.GetStringValue(); } },
                { "start_time", n => { StartTime = n.GetDateTimeOffsetValue(); } },
                { "status", n => { Status = n.GetDoubleValue(); } },
                { "tags", n => { Tags = n.GetObjectValue<global::Qyl.Core.Models.TraceQuery_tags>(global::Qyl.Core.Models.TraceQuery_tags.CreateFromDiscriminatorValue); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteStringValue("cursor", Cursor);
            writer.WriteDateTimeOffsetValue("end_time", EndTime);
            writer.WriteIntValue("limit", Limit);
            writer.WriteLongValue("max_duration_ms", MaxDurationMs);
            writer.WriteLongValue("min_duration_ms", MinDurationMs);
            writer.WriteStringValue("operation_name", OperationName);
            writer.WriteStringValue("query", Query);
            writer.WriteStringValue("service_name", ServiceName);
            writer.WriteDateTimeOffsetValue("start_time", StartTime);
            writer.WriteDoubleValue("status", Status);
            writer.WriteObjectValue<global::Qyl.Core.Models.TraceQuery_tags>("tags", Tags);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
