
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.OTel.Enums;

namespace Qyl.Domains.Observe.Log
{
    public partial class LogQuery
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public LogQuery()
        {
            AttributeFilters = new ChangeTrackingList<AttributeFilter>();
        }

        internal LogQuery(string query, SeverityNumber? severityMin, string serviceName, string traceId, string spanId, DateTimeOffset? timeStart, DateTimeOffset? timeEnd, IList<AttributeFilter> attributeFilters, int? limit, LogOrderBy? orderBy, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Query = query;
            SeverityMin = severityMin;
            ServiceName = serviceName;
            TraceId = traceId;
            SpanId = spanId;
            TimeStart = timeStart;
            TimeEnd = timeEnd;
            AttributeFilters = attributeFilters;
            Limit = limit;
            OrderBy = orderBy;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Query { get; set; }

        public SeverityNumber? SeverityMin { get; set; }

        public string ServiceName { get; set; }

        public string TraceId { get; set; }

        public string SpanId { get; set; }

        public DateTimeOffset? TimeStart { get; set; }

        public DateTimeOffset? TimeEnd { get; set; }

        public IList<AttributeFilter> AttributeFilters { get; }

        public int? Limit { get; set; }

        public LogOrderBy? OrderBy { get; set; }
    }
}
