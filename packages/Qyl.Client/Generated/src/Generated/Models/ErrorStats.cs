
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Client;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorStats(long totalCount, int uniqueTypes, double errorRate, IEnumerable<ErrorCategoryStats> byCategory, IEnumerable<ErrorTypeStats> topErrors, ErrorTrend trend)
        {
            TotalCount = totalCount;
            UniqueTypes = uniqueTypes;
            ErrorRate = errorRate;
            ByCategory = byCategory.ToList();
            ByService = new ChangeTrackingList<ErrorServiceStats>();
            TopErrors = topErrors.ToList();
            Trend = trend;
        }

        internal ErrorStats(long totalCount, int uniqueTypes, double errorRate, IList<ErrorCategoryStats> byCategory, IList<ErrorServiceStats> byService, IList<ErrorTypeStats> topErrors, ErrorTrend trend, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            TotalCount = totalCount;
            UniqueTypes = uniqueTypes;
            ErrorRate = errorRate;
            ByCategory = byCategory;
            ByService = byService;
            TopErrors = topErrors;
            Trend = trend;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public long TotalCount { get; }

        public int UniqueTypes { get; }

        public double ErrorRate { get; }

        public IList<ErrorCategoryStats> ByCategory { get; }

        public IList<ErrorServiceStats> ByService { get; }

        public IList<ErrorTypeStats> TopErrors { get; }

        public ErrorTrend Trend { get; }
    }
}
