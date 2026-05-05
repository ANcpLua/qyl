
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.Common;

namespace Qyl.OTel.Traces
{
    public partial class SpanLink
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SpanLink(string traceId, string spanId)
        {
            TraceId = traceId;
            SpanId = spanId;
            Attributes = new ChangeTrackingList<Common.Attribute>();
        }

        internal SpanLink(string traceId, string spanId, string traceState, IList<Common.Attribute> attributes, long? droppedAttributesCount, int? flags, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            TraceId = traceId;
            SpanId = spanId;
            TraceState = traceState;
            Attributes = attributes;
            DroppedAttributesCount = droppedAttributesCount;
            Flags = flags;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string TraceId { get; }

        public string SpanId { get; }

        public string TraceState { get; }

        public IList<Common.Attribute> Attributes { get; }

        public long? DroppedAttributesCount { get; }

        public int? Flags { get; }
    }
}
