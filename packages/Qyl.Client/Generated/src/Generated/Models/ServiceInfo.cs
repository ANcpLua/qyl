
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Api
{
    public partial class ServiceInfo
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ServiceInfo(string name, int instanceCount, DateTimeOffset lastSeen)
        {
            Name = name;
            InstanceCount = instanceCount;
            LastSeen = lastSeen;
        }

        internal ServiceInfo(string name, string namespaceName, string version, int instanceCount, DateTimeOffset lastSeen, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Name = name;
            NamespaceName = namespaceName;
            Version = version;
            InstanceCount = instanceCount;
            LastSeen = lastSeen;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Name { get; }

        public string NamespaceName { get; }

        public string Version { get; }

        public int InstanceCount { get; }

        public DateTimeOffset LastSeen { get; }
    }
}
