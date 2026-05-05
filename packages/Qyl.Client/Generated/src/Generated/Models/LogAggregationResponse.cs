
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Qyl.Api
{
    public partial class LogAggregationResponse
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal LogAggregationResponse(IEnumerable<LogAggregationBucket> results, long totalCount)
        {
            Results = results.ToList();
            TotalCount = totalCount;
        }

        internal LogAggregationResponse(IList<LogAggregationBucket> results, long totalCount, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Results = results;
            TotalCount = totalCount;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<LogAggregationBucket> Results { get; }

        public long TotalCount { get; }
    }
}
