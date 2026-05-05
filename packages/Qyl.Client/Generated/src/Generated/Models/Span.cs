
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.Common;
using Qyl.OTel.Enums;
using Qyl.OTel.Resource;

namespace Qyl.OTel.Traces
{
    public partial class Span
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal Span(string spanId, string traceId, string name, SpanKind kind, long startTimeUnixNano, long endTimeUnixNano, SpanStatus status, Resource.Resource resource)
        {
            SpanId = spanId;
            TraceId = traceId;
            Name = name;
            Kind = kind;
            StartTimeUnixNano = startTimeUnixNano;
            EndTimeUnixNano = endTimeUnixNano;
            Attributes = new ChangeTrackingList<Common.Attribute>();
            Events = new ChangeTrackingList<SpanEvent>();
            Links = new ChangeTrackingList<SpanLink>();
            Status = status;
            Resource = resource;
        }

        internal Span(string spanId, string traceId, string parentSpanId, string traceState, string name, SpanKind kind, long startTimeUnixNano, long endTimeUnixNano, IList<Common.Attribute> attributes, long? droppedAttributesCount, IList<SpanEvent> events, long? droppedEventsCount, IList<SpanLink> links, long? droppedLinksCount, SpanStatus status, int? flags, Resource.Resource resource, InstrumentationScope instrumentationScope, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            SpanId = spanId;
            TraceId = traceId;
            ParentSpanId = parentSpanId;
            TraceState = traceState;
            Name = name;
            Kind = kind;
            StartTimeUnixNano = startTimeUnixNano;
            EndTimeUnixNano = endTimeUnixNano;
            Attributes = attributes;
            DroppedAttributesCount = droppedAttributesCount;
            Events = events;
            DroppedEventsCount = droppedEventsCount;
            Links = links;
            DroppedLinksCount = droppedLinksCount;
            Status = status;
            Flags = flags;
            Resource = resource;
            InstrumentationScope = instrumentationScope;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string SpanId { get; }

        public string TraceId { get; }

        public string ParentSpanId { get; }

        public string TraceState { get; }

        public string Name { get; }

        public SpanKind Kind { get; }

        public long StartTimeUnixNano { get; }

        public long EndTimeUnixNano { get; }

        public IList<Common.Attribute> Attributes { get; }

        public long? DroppedAttributesCount { get; }

        public IList<SpanEvent> Events { get; }

        public long? DroppedEventsCount { get; }

        public IList<SpanLink> Links { get; }

        public long? DroppedLinksCount { get; }

        public SpanStatus Status { get; }

        public int? Flags { get; }

        public Resource.Resource Resource { get; }

        public InstrumentationScope InstrumentationScope { get; }
    }
}
