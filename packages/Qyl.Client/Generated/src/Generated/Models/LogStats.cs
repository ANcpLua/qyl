
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Qyl.OTel.Logs
{
    public partial class LogStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogStats(long totalCount, IEnumerable<LogCountBySeverity> bySeverity, IEnumerable<LogCountByDimension> byService, double logsPerSecond, double errorRate)
        {
            TotalCount = totalCount;
            BySeverity = bySeverity.ToList();
            ByService = byService.ToList();
            LogsPerSecond = logsPerSecond;
            ErrorRate = errorRate;
        }

        internal LogStats(long totalCount, IList<LogCountBySeverity> bySeverity, IList<LogCountByDimension> byService, double logsPerSecond, double errorRate, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            TotalCount = totalCount;
            BySeverity = bySeverity;
            ByService = byService;
            LogsPerSecond = logsPerSecond;
            ErrorRate = errorRate;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public long TotalCount { get; }

        public IList<LogCountBySeverity> BySeverity { get; }

        public IList<LogCountByDimension> ByService { get; }

        public double LogsPerSecond { get; }

        public double ErrorRate { get; }
    }
}
