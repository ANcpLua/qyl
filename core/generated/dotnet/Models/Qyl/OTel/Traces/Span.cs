
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Qyl.Core.Models.Qyl.Common;
using Qyl.Core.Models.Qyl.OTel.Resource;
using System.Collections.Generic;
using System.IO;
using System;
namespace Qyl.Core.Models.Qyl.OTel.Traces
{
        [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    public partial class Span : IAdditionalDataHolder, IParsable
    {
                public IDictionary<string, object> AdditionalData { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject>? Attributes { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.Common.AttributeObject> Attributes { get; set; }
#endif
                public long? DroppedAttributesCount { get; set; }
                public long? DroppedEventsCount { get; set; }
                public long? DroppedLinksCount { get; set; }
                public long? EndTimeUnixNano { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanEvent>? Events { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanEvent> Events { get; set; }
#endif
                public int? Flags { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.Common.InstrumentationScope? InstrumentationScope { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.Common.InstrumentationScope InstrumentationScope { get; set; }
#endif
                public double? Kind { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanLink>? Links { get; set; }
#nullable restore
#else
        public List<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanLink> Links { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? Name { get; set; }
#nullable restore
#else
        public string Name { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? ParentSpanId { get; set; }
#nullable restore
#else
        public string ParentSpanId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.OTel.Resource.Resource? Resource { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.OTel.Resource.Resource Resource { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? SpanId { get; set; }
#nullable restore
#else
        public string SpanId { get; set; }
#endif
                public long? StartTimeUnixNano { get; set; }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Qyl.Core.Models.Qyl.OTel.Traces.SpanStatus? Status { get; set; }
#nullable restore
#else
        public global::Qyl.Core.Models.Qyl.OTel.Traces.SpanStatus Status { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TraceId { get; set; }
#nullable restore
#else
        public string TraceId { get; set; }
#endif
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? TraceState { get; set; }
#nullable restore
#else
        public string TraceState { get; set; }
#endif
                public Span()
        {
            AdditionalData = new Dictionary<string, object>();
        }
                public static global::Qyl.Core.Models.Qyl.OTel.Traces.Span CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if(ReferenceEquals(parseNode, null)) throw new ArgumentNullException(nameof(parseNode));
            return new global::Qyl.Core.Models.Qyl.OTel.Traces.Span();
        }
                public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "attributes", n => { Attributes = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>(global::Qyl.Core.Models.Qyl.Common.AttributeObject.CreateFromDiscriminatorValue)?.AsList(); } },
                { "dropped_attributes_count", n => { DroppedAttributesCount = n.GetLongValue(); } },
                { "dropped_events_count", n => { DroppedEventsCount = n.GetLongValue(); } },
                { "dropped_links_count", n => { DroppedLinksCount = n.GetLongValue(); } },
                { "end_time_unix_nano", n => { EndTimeUnixNano = n.GetLongValue(); } },
                { "events", n => { Events = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanEvent>(global::Qyl.Core.Models.Qyl.OTel.Traces.SpanEvent.CreateFromDiscriminatorValue)?.AsList(); } },
                { "flags", n => { Flags = n.GetIntValue(); } },
                { "instrumentation_scope", n => { InstrumentationScope = n.GetObjectValue<global::Qyl.Core.Models.Qyl.Common.InstrumentationScope>(global::Qyl.Core.Models.Qyl.Common.InstrumentationScope.CreateFromDiscriminatorValue); } },
                { "kind", n => { Kind = n.GetDoubleValue(); } },
                { "links", n => { Links = n.GetCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanLink>(global::Qyl.Core.Models.Qyl.OTel.Traces.SpanLink.CreateFromDiscriminatorValue)?.AsList(); } },
                { "name", n => { Name = n.GetStringValue(); } },
                { "parent_span_id", n => { ParentSpanId = n.GetStringValue(); } },
                { "resource", n => { Resource = n.GetObjectValue<global::Qyl.Core.Models.Qyl.OTel.Resource.Resource>(global::Qyl.Core.Models.Qyl.OTel.Resource.Resource.CreateFromDiscriminatorValue); } },
                { "span_id", n => { SpanId = n.GetStringValue(); } },
                { "start_time_unix_nano", n => { StartTimeUnixNano = n.GetLongValue(); } },
                { "status", n => { Status = n.GetObjectValue<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanStatus>(global::Qyl.Core.Models.Qyl.OTel.Traces.SpanStatus.CreateFromDiscriminatorValue); } },
                { "trace_id", n => { TraceId = n.GetStringValue(); } },
                { "trace_state", n => { TraceState = n.GetStringValue(); } },
            };
        }
                public virtual void Serialize(ISerializationWriter writer)
        {
            if(ReferenceEquals(writer, null)) throw new ArgumentNullException(nameof(writer));
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.Common.AttributeObject>("attributes", Attributes);
            writer.WriteLongValue("dropped_attributes_count", DroppedAttributesCount);
            writer.WriteLongValue("dropped_events_count", DroppedEventsCount);
            writer.WriteLongValue("dropped_links_count", DroppedLinksCount);
            writer.WriteLongValue("end_time_unix_nano", EndTimeUnixNano);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanEvent>("events", Events);
            writer.WriteIntValue("flags", Flags);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.Common.InstrumentationScope>("instrumentation_scope", InstrumentationScope);
            writer.WriteDoubleValue("kind", Kind);
            writer.WriteCollectionOfObjectValues<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanLink>("links", Links);
            writer.WriteStringValue("name", Name);
            writer.WriteStringValue("parent_span_id", ParentSpanId);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.OTel.Resource.Resource>("resource", Resource);
            writer.WriteStringValue("span_id", SpanId);
            writer.WriteLongValue("start_time_unix_nano", StartTimeUnixNano);
            writer.WriteObjectValue<global::Qyl.Core.Models.Qyl.OTel.Traces.SpanStatus>("status", Status);
            writer.WriteStringValue("trace_id", TraceId);
            writer.WriteStringValue("trace_state", TraceState);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
