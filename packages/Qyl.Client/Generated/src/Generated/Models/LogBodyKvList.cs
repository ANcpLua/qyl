
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Common;

namespace Qyl.OTel.Logs
{
    public partial class LogBodyKvList
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogBodyKvList(IEnumerable<Common.Attribute> kvListValue)
        {
            KvListValue = kvListValue.ToList();
        }

        internal LogBodyKvList(IList<Common.Attribute> kvListValue, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            KvListValue = kvListValue;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<Common.Attribute> KvListValue { get; }
    }
}
