
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.Domains.Observe.Log;

namespace Qyl.Api
{
    public partial class LogAggregationRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public LogAggregationRequest(LogAggregation aggregation)
        {
            Argument.AssertNotNull(aggregation, nameof(aggregation));

            Aggregation = aggregation;
        }

        internal LogAggregationRequest(LogQuery query, LogAggregation aggregation, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Query = query;
            Aggregation = aggregation;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public LogQuery Query { get; set; }

        public LogAggregation Aggregation { get; }
    }
}
