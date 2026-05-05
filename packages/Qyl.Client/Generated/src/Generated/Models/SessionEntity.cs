
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SessionEntity(string sessionId, DateTimeOffset startTime, int traceCount, int spanCount, int errorCount, IEnumerable<string> services, SessionState state)
        {
            SessionId = sessionId;
            StartTime = startTime;
            TraceCount = traceCount;
            SpanCount = spanCount;
            ErrorCount = errorCount;
            Services = services.ToList();
            State = state;
        }

        internal SessionEntity(string sessionId, string userId, DateTimeOffset startTime, DateTimeOffset? endTime, double? durationMs, int traceCount, int spanCount, int errorCount, IList<string> services, SessionState state, SessionClientInfo client, SessionGeoInfo geo, SessionGenAiUsage genaiUsage, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            SessionId = sessionId;
            UserId = userId;
            StartTime = startTime;
            EndTime = endTime;
            DurationMs = durationMs;
            TraceCount = traceCount;
            SpanCount = spanCount;
            ErrorCount = errorCount;
            Services = services;
            State = state;
            Client = client;
            Geo = geo;
            GenaiUsage = genaiUsage;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string SessionId { get; }

        public string UserId { get; }

        public DateTimeOffset StartTime { get; }

        public DateTimeOffset? EndTime { get; }

        public double? DurationMs { get; }

        public int TraceCount { get; }

        public int SpanCount { get; }

        public int ErrorCount { get; }

        public IList<string> Services { get; }

        public SessionState State { get; }

        public SessionClientInfo Client { get; }

        public SessionGeoInfo Geo { get; }

        public SessionGenAiUsage GenaiUsage { get; }
    }
}
