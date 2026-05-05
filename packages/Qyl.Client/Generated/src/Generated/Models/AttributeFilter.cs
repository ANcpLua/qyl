
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Domains.Observe.Log
{
    public partial class AttributeFilter
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public AttributeFilter(string key, FilterOperator @operator, string value)
        {
            Argument.AssertNotNull(key, nameof(key));
            Argument.AssertNotNull(value, nameof(value));

            Key = key;
            Operator = @operator;
            Value = value;
        }

        internal AttributeFilter(string key, FilterOperator @operator, string value, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Key = key;
            Operator = @operator;
            Value = value;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Key { get; }

        public FilterOperator Operator { get; }

        public string Value { get; }
    }
}
