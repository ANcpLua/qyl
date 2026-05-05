
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.OTel.Logs
{
    public partial class LogBodyBytes
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogBodyBytes(BinaryData bytesValue)
        {
            BytesValue = bytesValue;
        }

        internal LogBodyBytes(BinaryData bytesValue, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            BytesValue = bytesValue;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public BinaryData BytesValue { get; }
    }
}
