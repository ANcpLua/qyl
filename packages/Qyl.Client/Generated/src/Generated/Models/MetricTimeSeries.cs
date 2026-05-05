
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Qyl.Api
{
    public partial class MetricTimeSeries
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal MetricTimeSeries(IDictionary<string, string> labels, IEnumerable<MetricDataPoint> points)
        {
            Labels = labels;
            Points = points.ToList();
        }

        internal MetricTimeSeries(IDictionary<string, string> labels, IList<MetricDataPoint> points, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Labels = labels;
            Points = points;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public IDictionary<string, string> Labels { get; }

        public IList<MetricDataPoint> Points { get; }
    }
}
