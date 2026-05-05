
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Api
{
    public partial class LogAggregationBucket
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogAggregationBucket(string key, double value, long count)
        {
            Key = key;
            Value = value;
            Count = count;
        }

        internal LogAggregationBucket(string key, double value, long count, DateTimeOffset? timestamp, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Key = key;
            Value = value;
            Count = count;
            Timestamp = timestamp;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Key { get; }

        public double Value { get; }

        public long Count { get; }

        public DateTimeOffset? Timestamp { get; }
    }
}
