
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.OTel.Enums;

namespace Qyl.Api
{
    public partial class OperationInfo
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal OperationInfo(string name, SpanKind spanKind, long requestCount, long errorCount, double avgDurationMs, double p99DurationMs)
        {
            Name = name;
            SpanKind = spanKind;
            RequestCount = requestCount;
            ErrorCount = errorCount;
            AvgDurationMs = avgDurationMs;
            P99DurationMs = p99DurationMs;
        }

        internal OperationInfo(string name, SpanKind spanKind, long requestCount, long errorCount, double avgDurationMs, double p99DurationMs, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Name = name;
            SpanKind = spanKind;
            RequestCount = requestCount;
            ErrorCount = errorCount;
            AvgDurationMs = avgDurationMs;
            P99DurationMs = p99DurationMs;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Name { get; }

        public SpanKind SpanKind { get; }

        public long RequestCount { get; }

        public long ErrorCount { get; }

        public double AvgDurationMs { get; }

        public double P99DurationMs { get; }
    }
}
