
#nullable disable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Common;
using Qyl.OTel.Enums;
using Qyl.OTel.Resource;

namespace Qyl.OTel.Logs
{
    public partial class LogRecord
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogRecord(long timeUnixNano, long observedTimeUnixNano, SeverityNumber severityNumber, BinaryData body, Resource.Resource resource)
        {
            TimeUnixNano = timeUnixNano;
            ObservedTimeUnixNano = observedTimeUnixNano;
            SeverityNumber = severityNumber;
            Body = body;
            Attributes = new ChangeTrackingList<Common.Attribute>();
            Resource = resource;
        }

        internal LogRecord(long timeUnixNano, long observedTimeUnixNano, SeverityNumber severityNumber, SeverityText? severityText, BinaryData body, IList<Common.Attribute> attributes, long? droppedAttributesCount, int? flags, string traceId, string spanId, Resource.Resource resource, InstrumentationScope instrumentationScope, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            TimeUnixNano = timeUnixNano;
            ObservedTimeUnixNano = observedTimeUnixNano;
            SeverityNumber = severityNumber;
            SeverityText = severityText;
            Body = body;
            Attributes = attributes;
            DroppedAttributesCount = droppedAttributesCount;
            Flags = flags;
            TraceId = traceId;
            SpanId = spanId;
            Resource = resource;
            InstrumentationScope = instrumentationScope;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public long TimeUnixNano { get; }

        public long ObservedTimeUnixNano { get; }

        public SeverityNumber SeverityNumber { get; }

        public SeverityText? SeverityText { get; }

        public BinaryData Body { get; }

        public IList<Common.Attribute> Attributes { get; }

        public long? DroppedAttributesCount { get; }

        public int? Flags { get; }

        public string TraceId { get; }

        public string SpanId { get; }

        public Resource.Resource Resource { get; }

        public InstrumentationScope InstrumentationScope { get; }
    }
}
