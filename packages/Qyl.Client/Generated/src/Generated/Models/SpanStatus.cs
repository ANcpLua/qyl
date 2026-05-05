
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.OTel.Enums;

namespace Qyl.OTel.Traces
{
    public partial class SpanStatus
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SpanStatus(SpanStatusCode code)
        {
            Code = code;
        }

        internal SpanStatus(SpanStatusCode code, string message, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Code = code;
            Message = message;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public SpanStatusCode Code { get; }

        public string Message { get; }
    }
}
