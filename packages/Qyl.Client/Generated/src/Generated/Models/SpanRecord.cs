
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.OTel.Enums;

namespace Qyl.Storage
{
    public partial class SpanRecord
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SpanRecord(string spanId, string traceId, string name, SpanKind kind, long startTimeUnixNano, long endTimeUnixNano, long durationNs, SpanStatusCode statusCode)
        {
            SpanId = spanId;
            TraceId = traceId;
            Name = name;
            Kind = kind;
            StartTimeUnixNano = startTimeUnixNano;
            EndTimeUnixNano = endTimeUnixNano;
            DurationNs = durationNs;
            StatusCode = statusCode;
        }

        internal SpanRecord(string spanId, string traceId, string parentSpanId, string sessionId, string name, SpanKind kind, long startTimeUnixNano, long endTimeUnixNano, long durationNs, SpanStatusCode statusCode, string statusMessage, string serviceName, string genAiProviderName, string genAiRequestModel, string genAiResponseModel, long? genAiInputTokens, long? genAiOutputTokens, double? genAiTemperature, string genAiStopReason, string genAiToolName, string genAiToolCallId, double? genAiCostUsd, string attributesJson, string resourceJson, string baggageJson, string schemaUrl, DateTimeOffset? createdAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            SpanId = spanId;
            TraceId = traceId;
            ParentSpanId = parentSpanId;
            SessionId = sessionId;
            Name = name;
            Kind = kind;
            StartTimeUnixNano = startTimeUnixNano;
            EndTimeUnixNano = endTimeUnixNano;
            DurationNs = durationNs;
            StatusCode = statusCode;
            StatusMessage = statusMessage;
            ServiceName = serviceName;
            GenAiProviderName = genAiProviderName;
            GenAiRequestModel = genAiRequestModel;
            GenAiResponseModel = genAiResponseModel;
            GenAiInputTokens = genAiInputTokens;
            GenAiOutputTokens = genAiOutputTokens;
            GenAiTemperature = genAiTemperature;
            GenAiStopReason = genAiStopReason;
            GenAiToolName = genAiToolName;
            GenAiToolCallId = genAiToolCallId;
            GenAiCostUsd = genAiCostUsd;
            AttributesJson = attributesJson;
            ResourceJson = resourceJson;
            BaggageJson = baggageJson;
            SchemaUrl = schemaUrl;
            CreatedAt = createdAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string SpanId { get; }

        public string TraceId { get; }

        public string ParentSpanId { get; }

        public string SessionId { get; }

        public string Name { get; }

        public SpanKind Kind { get; }

        public long StartTimeUnixNano { get; }

        public long EndTimeUnixNano { get; }

        public long DurationNs { get; }

        public SpanStatusCode StatusCode { get; }

        public string StatusMessage { get; }

        public string ServiceName { get; }

        public string GenAiProviderName { get; }

        public string GenAiRequestModel { get; }

        public string GenAiResponseModel { get; }

        public long? GenAiInputTokens { get; }

        public long? GenAiOutputTokens { get; }

        public double? GenAiTemperature { get; }

        public string GenAiStopReason { get; }

        public string GenAiToolName { get; }

        public string GenAiToolCallId { get; }

        public double? GenAiCostUsd { get; }

        public string AttributesJson { get; }

        public string ResourceJson { get; }

        public string BaggageJson { get; }

        public string SchemaUrl { get; }

        public DateTimeOffset? CreatedAt { get; }
    }
}
