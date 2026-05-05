
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Api
{
    public partial class MetricDataPoint
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal MetricDataPoint(DateTimeOffset timestamp, double value)
        {
            Timestamp = timestamp;
            Value = value;
        }

        internal MetricDataPoint(DateTimeOffset timestamp, double value, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Timestamp = timestamp;
            Value = value;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public DateTimeOffset Timestamp { get; }

        public double Value { get; }
    }
}
