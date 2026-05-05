
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.Common.Pagination;
using Qyl.OTel.Metrics;

namespace Qyl.Api
{
    public partial class MetricQueryRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public MetricQueryRequest(string metricName, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            Argument.AssertNotNull(metricName, nameof(metricName));

            MetricName = metricName;
            Filters = new ChangeTrackingDictionary<string, string>();
            StartTime = startTime;
            EndTime = endTime;
            GroupBy = new ChangeTrackingList<string>();
        }

        internal MetricQueryRequest(string metricName, IDictionary<string, string> filters, DateTimeOffset startTime, DateTimeOffset endTime, TimeBucket? step, AggregationFunction? aggregation, IList<string> groupBy, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            MetricName = metricName;
            Filters = filters;
            StartTime = startTime;
            EndTime = endTime;
            Step = step;
            Aggregation = aggregation;
            GroupBy = groupBy;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string MetricName { get; }

        public IDictionary<string, string> Filters { get; }

        public DateTimeOffset StartTime { get; }

        public DateTimeOffset EndTime { get; }

        public TimeBucket? Step { get; set; }

        public AggregationFunction? Aggregation { get; set; }

        public IList<string> GroupBy { get; }
    }
}
