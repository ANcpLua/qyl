
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.OTel.Enums;

namespace Qyl.Api
{
    public partial class TraceQuery
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public TraceQuery()
        {
            Tags = new ChangeTrackingDictionary<string, string>();
        }

        internal TraceQuery(string query, string serviceName, string operationName, long? minDurationMs, long? maxDurationMs, SpanStatusCode? status, DateTimeOffset? startTime, DateTimeOffset? endTime, IDictionary<string, string> tags, int? limit, string cursor, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Query = query;
            ServiceName = serviceName;
            OperationName = operationName;
            MinDurationMs = minDurationMs;
            MaxDurationMs = maxDurationMs;
            Status = status;
            StartTime = startTime;
            EndTime = endTime;
            Tags = tags;
            Limit = limit;
            Cursor = cursor;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Query { get; set; }

        public string ServiceName { get; set; }

        public string OperationName { get; set; }

        public long? MinDurationMs { get; set; }

        public long? MaxDurationMs { get; set; }

        public SpanStatusCode? Status { get; set; }

        public DateTimeOffset? StartTime { get; set; }

        public DateTimeOffset? EndTime { get; set; }

        public IDictionary<string, string> Tags { get; }

        public int? Limit { get; set; }

        public string Cursor { get; set; }
    }
}
