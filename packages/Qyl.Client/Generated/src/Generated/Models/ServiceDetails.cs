
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Common;

namespace Qyl.Api
{
    public partial class ServiceDetails
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ServiceDetails(string name, int instanceCount, DateTimeOffset lastSeen, IEnumerable<Common.Attribute> resourceAttributes, IEnumerable<InstrumentationScope> instrumentationLibraries, double requestRate, double errorRate, double avgLatencyMs, double p99LatencyMs)
        {
            Name = name;
            InstanceCount = instanceCount;
            LastSeen = lastSeen;
            ResourceAttributes = resourceAttributes.ToList();
            InstrumentationLibraries = instrumentationLibraries.ToList();
            RequestRate = requestRate;
            ErrorRate = errorRate;
            AvgLatencyMs = avgLatencyMs;
            P99LatencyMs = p99LatencyMs;
        }

        internal ServiceDetails(string name, string namespaceName, string version, int instanceCount, DateTimeOffset lastSeen, IList<Common.Attribute> resourceAttributes, IList<InstrumentationScope> instrumentationLibraries, double requestRate, double errorRate, double avgLatencyMs, double p99LatencyMs, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Name = name;
            NamespaceName = namespaceName;
            Version = version;
            InstanceCount = instanceCount;
            LastSeen = lastSeen;
            ResourceAttributes = resourceAttributes;
            InstrumentationLibraries = instrumentationLibraries;
            RequestRate = requestRate;
            ErrorRate = errorRate;
            AvgLatencyMs = avgLatencyMs;
            P99LatencyMs = p99LatencyMs;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Name { get; }

        public string NamespaceName { get; }

        public string Version { get; }

        public int InstanceCount { get; }

        public DateTimeOffset LastSeen { get; }

        public IList<Common.Attribute> ResourceAttributes { get; }

        public IList<InstrumentationScope> InstrumentationLibraries { get; }

        public double RequestRate { get; }

        public double ErrorRate { get; }

        public double AvgLatencyMs { get; }

        public double P99LatencyMs { get; }
    }
}
