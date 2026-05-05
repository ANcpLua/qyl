
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.OTel.Logs
{
    public partial class LogBodyString
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogBodyString(string stringValue)
        {
            StringValue = stringValue;
        }

        internal LogBodyString(string stringValue, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            StringValue = stringValue;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string StringValue { get; }
    }
}
