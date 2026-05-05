
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Common
{
    public partial class InstrumentationScope
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal InstrumentationScope(string scopeName)
        {
            ScopeName = scopeName;
            ScopeAttributes = new ChangeTrackingList<Attribute>();
        }

        internal InstrumentationScope(string scopeName, string scopeVersion, IList<Attribute> scopeAttributes, long? droppedAttributesCount, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ScopeName = scopeName;
            ScopeVersion = scopeVersion;
            ScopeAttributes = scopeAttributes;
            DroppedAttributesCount = droppedAttributesCount;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ScopeName { get; }

        public string ScopeVersion { get; }

        public IList<Attribute> ScopeAttributes { get; }

        public long? DroppedAttributesCount { get; }
    }
}
