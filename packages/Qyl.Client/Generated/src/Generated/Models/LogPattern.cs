
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Domains.Observe.Log
{
    public partial class LogPattern
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogPattern(string patternId, string template, string sample, long count, DateTimeOffset firstSeen, DateTimeOffset lastSeen, LogPatternTrend trend)
        {
            PatternId = patternId;
            Template = template;
            Sample = sample;
            Count = count;
            FirstSeen = firstSeen;
            LastSeen = lastSeen;
            Trend = trend;
            SeverityDistribution = new ChangeTrackingList<LogSeverityStats>();
        }

        internal LogPattern(string patternId, string template, string sample, long count, DateTimeOffset firstSeen, DateTimeOffset lastSeen, LogPatternTrend trend, IList<LogSeverityStats> severityDistribution, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            PatternId = patternId;
            Template = template;
            Sample = sample;
            Count = count;
            FirstSeen = firstSeen;
            LastSeen = lastSeen;
            Trend = trend;
            SeverityDistribution = severityDistribution;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string PatternId { get; }

        public string Template { get; }

        public string Sample { get; }

        public long Count { get; }

        public DateTimeOffset FirstSeen { get; }

        public DateTimeOffset LastSeen { get; }

        public LogPatternTrend Trend { get; }

        public IList<LogSeverityStats> SeverityDistribution { get; }
    }
}
