
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.OTel.Enums;

namespace Qyl.OTel.Logs
{
    public partial class LogCountBySeverity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogCountBySeverity(SeverityText severity, long count, double percentage)
        {
            Severity = severity;
            Count = count;
            Percentage = percentage;
        }

        internal LogCountBySeverity(SeverityText severity, long count, double percentage, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Severity = severity;
            Count = count;
            Percentage = percentage;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public SeverityText Severity { get; }

        public long Count { get; }

        public double Percentage { get; }
    }
}
