
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Qyl.Api
{
    public partial class MetricQueryResponse
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal MetricQueryResponse(string metricName, IEnumerable<MetricTimeSeries> series)
        {
            MetricName = metricName;
            Series = series.ToList();
        }

        internal MetricQueryResponse(string metricName, IList<MetricTimeSeries> series, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            MetricName = metricName;
            Series = series;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string MetricName { get; }

        public IList<MetricTimeSeries> Series { get; }
    }
}
