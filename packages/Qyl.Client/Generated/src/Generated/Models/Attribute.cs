
#nullable disable

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Qyl.Common
{
    public partial class Attribute
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal Attribute(string key, BinaryData value)
        {
            Key = key;
            Value = value;
        }

        internal Attribute(string key, BinaryData value, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Key = key;
            Value = value;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Key { get; }

        public BinaryData Value { get; }
    }
}
