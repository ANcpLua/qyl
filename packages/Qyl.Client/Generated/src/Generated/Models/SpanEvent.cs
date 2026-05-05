
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.Common;

namespace Qyl.OTel.Traces
{
    public partial class SpanEvent
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SpanEvent(string name, long timeUnixNano)
        {
            Name = name;
            TimeUnixNano = timeUnixNano;
            Attributes = new ChangeTrackingList<Common.Attribute>();
        }

        internal SpanEvent(string name, long timeUnixNano, IList<Common.Attribute> attributes, long? droppedAttributesCount, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Name = name;
            TimeUnixNano = timeUnixNano;
            Attributes = attributes;
            DroppedAttributesCount = droppedAttributesCount;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Name { get; }

        public long TimeUnixNano { get; }

        public IList<Common.Attribute> Attributes { get; }

        public long? DroppedAttributesCount { get; }
    }
}
