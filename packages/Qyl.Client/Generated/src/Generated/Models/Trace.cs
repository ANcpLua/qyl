
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Qyl.OTel.Traces
{
    public partial class Trace
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal Trace(string traceId, IEnumerable<Span> spans, int spanCount, long durationNs, DateTimeOffset startTime, DateTimeOffset endTime, IEnumerable<string> services, bool hasError)
        {
            TraceId = traceId;
            Spans = spans.ToList();
            SpanCount = spanCount;
            DurationNs = durationNs;
            StartTime = startTime;
            EndTime = endTime;
            Services = services.ToList();
            HasError = hasError;
        }

        internal Trace(string traceId, IList<Span> spans, Span rootSpan, int spanCount, long durationNs, DateTimeOffset startTime, DateTimeOffset endTime, IList<string> services, bool hasError, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            TraceId = traceId;
            Spans = spans;
            RootSpan = rootSpan;
            SpanCount = spanCount;
            DurationNs = durationNs;
            StartTime = startTime;
            EndTime = endTime;
            Services = services;
            HasError = hasError;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string TraceId { get; }

        public IList<Span> Spans { get; }

        public Span RootSpan { get; }

        public int SpanCount { get; }

        public long DurationNs { get; }

        public DateTimeOffset StartTime { get; }

        public DateTimeOffset EndTime { get; }

        public IList<string> Services { get; }

        public bool HasError { get; }
    }
}
