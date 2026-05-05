
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Identity
{
    public partial class ServiceDependency
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ServiceDependency(string sourceService, string targetService, long requestCount, double errorRate, double avgLatencyMs)
        {
            SourceService = sourceService;
            TargetService = targetService;
            RequestCount = requestCount;
            ErrorRate = errorRate;
            AvgLatencyMs = avgLatencyMs;
        }

        internal ServiceDependency(string sourceService, string targetService, long requestCount, double errorRate, double avgLatencyMs, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            SourceService = sourceService;
            TargetService = targetService;
            RequestCount = requestCount;
            ErrorRate = errorRate;
            AvgLatencyMs = avgLatencyMs;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string SourceService { get; }

        public string TargetService { get; }

        public long RequestCount { get; }

        public double ErrorRate { get; }

        public double AvgLatencyMs { get; }
    }
}
