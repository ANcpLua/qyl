
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Qyl.OTel.Logs
{
    public partial class LogBodyArray
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogBodyArray(IEnumerable<BinaryData> arrayValue)
        {
            ArrayValue = arrayValue.ToList();
        }

        internal LogBodyArray(IList<BinaryData> arrayValue, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ArrayValue = arrayValue;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<BinaryData> ArrayValue { get; }
    }
}
