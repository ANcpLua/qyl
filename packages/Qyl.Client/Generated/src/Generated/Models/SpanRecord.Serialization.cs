
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.OTel.Enums;

namespace Qyl.Storage
{
    public partial class SpanRecord : IJsonModel<SpanRecord>
    {
        internal SpanRecord()
        {
        }

        protected virtual SpanRecord PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanRecord>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSpanRecord(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SpanRecord)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanRecord>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SpanRecord)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SpanRecord>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SpanRecord IPersistableModel<SpanRecord>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SpanRecord>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<SpanRecord>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanRecord>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SpanRecord)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("spanId"u8);
            writer.WriteStringValue(SpanId);
            writer.WritePropertyName("traceId"u8);
            writer.WriteStringValue(TraceId);
            if (Optional.IsDefined(ParentSpanId))
            {
                writer.WritePropertyName("parentSpanId"u8);
                writer.WriteStringValue(ParentSpanId);
            }
            if (Optional.IsDefined(SessionId))
            {
                writer.WritePropertyName("sessionId"u8);
                writer.WriteStringValue(SessionId);
            }
            writer.WritePropertyName("name"u8);
            writer.WriteStringValue(Name);
            writer.WritePropertyName("kind"u8);
            writer.WriteNumberValue((int)Kind);
            writer.WritePropertyName("startTimeUnixNano"u8);
            writer.WriteNumberValue(StartTimeUnixNano);
            writer.WritePropertyName("endTimeUnixNano"u8);
            writer.WriteNumberValue(EndTimeUnixNano);
            writer.WritePropertyName("durationNs"u8);
            writer.WriteNumberValue(DurationNs);
            writer.WritePropertyName("statusCode"u8);
            writer.WriteNumberValue((int)StatusCode);
            if (Optional.IsDefined(StatusMessage))
            {
                writer.WritePropertyName("statusMessage"u8);
                writer.WriteStringValue(StatusMessage);
            }
            if (Optional.IsDefined(ServiceName))
            {
                writer.WritePropertyName("serviceName"u8);
                writer.WriteStringValue(ServiceName);
            }
            if (Optional.IsDefined(GenAiProviderName))
            {
                writer.WritePropertyName("genAiProviderName"u8);
                writer.WriteStringValue(GenAiProviderName);
            }
            if (Optional.IsDefined(GenAiRequestModel))
            {
                writer.WritePropertyName("genAiRequestModel"u8);
                writer.WriteStringValue(GenAiRequestModel);
            }
            if (Optional.IsDefined(GenAiResponseModel))
            {
                writer.WritePropertyName("genAiResponseModel"u8);
                writer.WriteStringValue(GenAiResponseModel);
            }
            if (Optional.IsDefined(GenAiInputTokens))
            {
                writer.WritePropertyName("genAiInputTokens"u8);
                writer.WriteNumberValue(GenAiInputTokens.Value);
            }
            if (Optional.IsDefined(GenAiOutputTokens))
            {
                writer.WritePropertyName("genAiOutputTokens"u8);
                writer.WriteNumberValue(GenAiOutputTokens.Value);
            }
            if (Optional.IsDefined(GenAiTemperature))
            {
                writer.WritePropertyName("genAiTemperature"u8);
                writer.WriteNumberValue(GenAiTemperature.Value);
            }
            if (Optional.IsDefined(GenAiStopReason))
            {
                writer.WritePropertyName("genAiStopReason"u8);
                writer.WriteStringValue(GenAiStopReason);
            }
            if (Optional.IsDefined(GenAiToolName))
            {
                writer.WritePropertyName("genAiToolName"u8);
                writer.WriteStringValue(GenAiToolName);
            }
            if (Optional.IsDefined(GenAiToolCallId))
            {
                writer.WritePropertyName("genAiToolCallId"u8);
                writer.WriteStringValue(GenAiToolCallId);
            }
            if (Optional.IsDefined(GenAiCostUsd))
            {
                writer.WritePropertyName("genAiCostUsd"u8);
                writer.WriteNumberValue(GenAiCostUsd.Value);
            }
            if (Optional.IsDefined(AttributesJson))
            {
                writer.WritePropertyName("attributesJson"u8);
                writer.WriteStringValue(AttributesJson);
            }
            if (Optional.IsDefined(ResourceJson))
            {
                writer.WritePropertyName("resourceJson"u8);
                writer.WriteStringValue(ResourceJson);
            }
            if (Optional.IsDefined(BaggageJson))
            {
                writer.WritePropertyName("baggageJson"u8);
                writer.WriteStringValue(BaggageJson);
            }
            if (Optional.IsDefined(SchemaUrl))
            {
                writer.WritePropertyName("schemaUrl"u8);
                writer.WriteStringValue(SchemaUrl);
            }
            if (Optional.IsDefined(CreatedAt))
            {
                writer.WritePropertyName("createdAt"u8);
                writer.WriteStringValue(CreatedAt.Value, "O");
            }
            if (options.Format != "W" && _additionalBinaryDataProperties != null)
            {
                foreach (var item in _additionalBinaryDataProperties)
                {
                    writer.WritePropertyName(item.Key);
#if NET6_0_OR_GREATER
                    writer.WriteRawValue(item.Value);
#else
                    using (JsonDocument document = JsonDocument.Parse(item.Value))
                    {
                        JsonSerializer.Serialize(writer, document.RootElement);
                    }
#endif
                }
            }
        }

        SpanRecord IJsonModel<SpanRecord>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SpanRecord JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanRecord>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SpanRecord)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSpanRecord(document.RootElement, options);
        }

        internal static SpanRecord DeserializeSpanRecord(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string spanId = default;
            string traceId = default;
            string parentSpanId = default;
            string sessionId = default;
            string name = default;
            SpanKind kind = default;
            long startTimeUnixNano = default;
            long endTimeUnixNano = default;
            long durationNs = default;
            SpanStatusCode statusCode = default;
            string statusMessage = default;
            string serviceName = default;
            string genAiProviderName = default;
            string genAiRequestModel = default;
            string genAiResponseModel = default;
            long? genAiInputTokens = default;
            long? genAiOutputTokens = default;
            double? genAiTemperature = default;
            string genAiStopReason = default;
            string genAiToolName = default;
            string genAiToolCallId = default;
            double? genAiCostUsd = default;
            string attributesJson = default;
            string resourceJson = default;
            string baggageJson = default;
            string schemaUrl = default;
            DateTimeOffset? createdAt = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("spanId"u8))
                {
                    spanId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("traceId"u8))
                {
                    traceId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("parentSpanId"u8))
                {
                    parentSpanId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("sessionId"u8))
                {
                    sessionId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("name"u8))
                {
                    name = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("kind"u8))
                {
                    kind = prop.Value.GetInt32().ToSpanKind();
                    continue;
                }
                if (prop.NameEquals("startTimeUnixNano"u8))
                {
                    startTimeUnixNano = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("endTimeUnixNano"u8))
                {
                    endTimeUnixNano = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("durationNs"u8))
                {
                    durationNs = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("statusCode"u8))
                {
                    statusCode = prop.Value.GetInt32().ToSpanStatusCode();
                    continue;
                }
                if (prop.NameEquals("statusMessage"u8))
                {
                    statusMessage = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("serviceName"u8))
                {
                    serviceName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("genAiProviderName"u8))
                {
                    genAiProviderName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("genAiRequestModel"u8))
                {
                    genAiRequestModel = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("genAiResponseModel"u8))
                {
                    genAiResponseModel = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("genAiInputTokens"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    genAiInputTokens = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("genAiOutputTokens"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    genAiOutputTokens = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("genAiTemperature"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    genAiTemperature = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("genAiStopReason"u8))
                {
                    genAiStopReason = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("genAiToolName"u8))
                {
                    genAiToolName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("genAiToolCallId"u8))
                {
                    genAiToolCallId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("genAiCostUsd"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    genAiCostUsd = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("attributesJson"u8))
                {
                    attributesJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("resourceJson"u8))
                {
                    resourceJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("baggageJson"u8))
                {
                    baggageJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("schemaUrl"u8))
                {
                    schemaUrl = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("createdAt"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    createdAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SpanRecord(
                spanId,
                traceId,
                parentSpanId,
                sessionId,
                name,
                kind,
                startTimeUnixNano,
                endTimeUnixNano,
                durationNs,
                statusCode,
                statusMessage,
                serviceName,
                genAiProviderName,
                genAiRequestModel,
                genAiResponseModel,
                genAiInputTokens,
                genAiOutputTokens,
                genAiTemperature,
                genAiStopReason,
                genAiToolName,
                genAiToolCallId,
                genAiCostUsd,
                attributesJson,
                resourceJson,
                baggageJson,
                schemaUrl,
                createdAt,
                additionalBinaryDataProperties);
        }
    }
}
