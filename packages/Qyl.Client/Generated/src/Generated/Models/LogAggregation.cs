
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Client;
using Qyl.OTel.Metrics;
using Qyl.Common.Pagination;

namespace Qyl.Domains.Observe.Log
{
    public partial class LogAggregation
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public LogAggregation(IEnumerable<string> groupBy, AggregationFunction function)
        {
            Argument.AssertNotNull(groupBy, nameof(groupBy));

            GroupBy = groupBy.ToList();
            Function = function;
        }

        internal LogAggregation(IList<string> groupBy, AggregationFunction function, string @field, TimeBucket? timeBucket, int? topN, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            GroupBy = groupBy;
            Function = function;
            Field = @field;
            TimeBucket = timeBucket;
            TopN = topN;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IList<string> GroupBy { get; }

        public AggregationFunction Function { get; }

        public string Field { get; set; }

        public TimeBucket? TimeBucket { get; set; }

        public int? TopN { get; set; }
    }
}
