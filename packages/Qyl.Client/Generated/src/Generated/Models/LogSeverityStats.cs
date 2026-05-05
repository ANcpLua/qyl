
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.OTel.Enums;

namespace Qyl.Domains.Observe.Log
{
    public partial class LogSeverityStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogSeverityStats(SeverityNumber severity, string severityText, long count, double percentage)
        {
            Severity = severity;
            SeverityText = severityText;
            Count = count;
            Percentage = percentage;
        }

        internal LogSeverityStats(SeverityNumber severity, string severityText, long count, double percentage, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Severity = severity;
            SeverityText = severityText;
            Count = count;
            Percentage = percentage;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public SeverityNumber Severity { get; }

        public string SeverityText { get; }

        public long Count { get; }

        public double Percentage { get; }
    }
}
