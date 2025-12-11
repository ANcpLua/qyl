
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.OTel.Traces
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class Trace : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
                public long? DurationNs { get; set; }
                public DateTimeOffset? EndTime { get; set; }
                public bool? HasError { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.OTel.Traces.Span? RootSpan { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.OTel.Traces.Span RootSpan { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<string>? Services { get; set; }
#nullable restore
#else
        public List<string> Services { get; set; }
#endif
                public int? SpanCount { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.OTel.Traces.Span>? Spans { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.OTel.Traces.Span> Spans { get; set; }
#endif
                public DateTimeOffset? StartTime { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TraceId { get; set; }
#nullable restore
#else
        public string TraceId { get; set; }
#endif
                public Trace()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.OTel.Traces.Trace CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.OTel.Traces.Trace();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "duration_ns", n => { DurationNs = n.GetLongValue(); } },
                { "end_time", n => { EndTime = n.GetDateTimeOffsetValue(); } },
                { "has_error", n => { HasError = n.GetBoolValue(); } },
                { "root_span", n => { RootSpan = n.GetObjectValue<global::Qyl.Core.Models.Qyl.OTel.Traces.Span>(global::Qyl.Core.Models.Qyl.OTel.Traces.Span.CreateFromDiscriminatorValue); } },
                { "services", n => { Services = n.GetCollectionOfPrimitiveValues<string>()?.AsList(); } },
                { "span_count", n => { SpanCount = n.GetIntValue(); } },
                { "spans", n => { Spans = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.OTel.Traces.Span>(global::Qyl.Core.Models.Qyl.OTel.Traces.Span.CreateFromDiscriminatorValue)?.AsList(); } },
                { "start_time", n => { StartTime = n.GetDateTimeOffsetValue(); } },
                { "trace_id", n => { TraceId = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteLongValue("duration_ns", DurationNs);
            writer.WriteDateTimeOffsetValue("end_time", EndTime);
            writer.WriteBoolValue("has_error", HasError);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.OTel.Traces.Span>("root_span", RootSpan);
            writer.WriteCollectionOfPrimitiveValues<string>("services", Services);
            writer.WriteIntValue("span_count", SpanCount);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.OTel.Traces.Span>("spans", Spans);
            writer.WriteDateTimeOffsetValue("start_time", StartTime);
            writer.WriteStringValue("trace_id", TraceId);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
