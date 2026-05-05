
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.OTel.Logs
{
    public partial class LogCountByDimension
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogCountByDimension(string dimension, long count, long errorCount)
        {
            Dimension = dimension;
            Count = count;
            ErrorCount = errorCount;
        }

        internal LogCountByDimension(string dimension, long count, long errorCount, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Dimension = dimension;
            Count = count;
            ErrorCount = errorCount;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Dimension { get; }

        public long Count { get; }

        public long ErrorCount { get; }
    }
}
