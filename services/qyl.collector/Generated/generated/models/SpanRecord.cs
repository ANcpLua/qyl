
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;

namespace Qyl.Storage
{

    public partial class SpanRecord
    {
        [StringConstraint(MinLength = 16, MaxLength = 16, Pattern = "^[a-f0-9]{16}$")]
        public string SpanId { get; set; }

        [StringConstraint(MinLength = 32, MaxLength = 32, Pattern = "^[a-f0-9]{32}$")]
        public string TraceId { get; set; }

        [StringConstraint(MinLength = 16, MaxLength = 16, Pattern = "^[a-f0-9]{16}$")]
        public string ParentSpanId { get; set; }

        [StringConstraint(MinLength = 1, MaxLength = 128)]
        public string SessionId { get; set; }

        [StringConstraint(MinLength = 1)]
        public string Name { get; set; }

        public SpanKind Kind { get; set; }

        public long StartTimeUnixNano { get; set; }

        public long EndTimeUnixNano { get; set; }

        public long DurationNs { get; set; }

        public SpanStatusCode StatusCode { get; set; }

        public string StatusMessage { get; set; }

        public string ServiceName { get; set; }

        public string GenAiProviderName { get; set; }

        public string GenAiRequestModel { get; set; }

        public string GenAiResponseModel { get; set; }

        public long? GenAiInputTokens { get; set; }

        public long? GenAiOutputTokens { get; set; }

        public double? GenAiTemperature { get; set; }

        public string GenAiStopReason { get; set; }

        public string GenAiToolName { get; set; }

        public string GenAiToolCallId { get; set; }

        public double? GenAiCostUsd { get; set; }

        public string AttributesJson { get; set; }

        public string ResourceJson { get; set; }

        public string BaggageJson { get; set; }

        public string SchemaUrl { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }


    }
}
