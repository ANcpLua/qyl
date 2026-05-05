
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorServiceStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorServiceStats(string serviceName, long count, double errorRate, string topErrorType)
        {
            ServiceName = serviceName;
            Count = count;
            ErrorRate = errorRate;
            TopErrorType = topErrorType;
        }

        internal ErrorServiceStats(string serviceName, long count, double errorRate, string topErrorType, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ServiceName = serviceName;
            Count = count;
            ErrorRate = errorRate;
            TopErrorType = topErrorType;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ServiceName { get; }

        public long Count { get; }

        public double ErrorRate { get; }

        public string TopErrorType { get; }
    }
}
