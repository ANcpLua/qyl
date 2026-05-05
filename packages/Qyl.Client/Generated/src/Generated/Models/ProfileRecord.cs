
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Storage
{
    public partial class ProfileRecord
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ProfileRecord(string profileId, long timeUnixNano, long durationNano, int sampleCount)
        {
            ProfileId = profileId;
            TimeUnixNano = timeUnixNano;
            DurationNano = durationNano;
            SampleCount = sampleCount;
        }

        internal ProfileRecord(string profileId, string traceId, string spanId, string sessionId, long timeUnixNano, long durationNano, int sampleCount, string sampleType, string sampleUnit, string originalPayloadFormat, string serviceName, string profileFrameType, string attributesJson, string resourceJson, string profileDataJson, string schemaUrl, DateTimeOffset? createdAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ProfileId = profileId;
            TraceId = traceId;
            SpanId = spanId;
            SessionId = sessionId;
            TimeUnixNano = timeUnixNano;
            DurationNano = durationNano;
            SampleCount = sampleCount;
            SampleType = sampleType;
            SampleUnit = sampleUnit;
            OriginalPayloadFormat = originalPayloadFormat;
            ServiceName = serviceName;
            ProfileFrameType = profileFrameType;
            AttributesJson = attributesJson;
            ResourceJson = resourceJson;
            ProfileDataJson = profileDataJson;
            SchemaUrl = schemaUrl;
            CreatedAt = createdAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ProfileId { get; }

        public string TraceId { get; }

        public string SpanId { get; }

        public string SessionId { get; }

        public long TimeUnixNano { get; }

        public long DurationNano { get; }

        public int SampleCount { get; }

        public string SampleType { get; }

        public string SampleUnit { get; }

        public string OriginalPayloadFormat { get; }

        public string ServiceName { get; }

        public string ProfileFrameType { get; }

        public string AttributesJson { get; }

        public string ResourceJson { get; }

        public string ProfileDataJson { get; }

        public string SchemaUrl { get; }

        public DateTimeOffset? CreatedAt { get; }
    }
}
