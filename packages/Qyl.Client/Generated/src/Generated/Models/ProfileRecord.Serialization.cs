
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Storage
{
    public partial class ProfileRecord : IJsonModel<ProfileRecord>
    {
        internal ProfileRecord()
        {
        }

        protected virtual ProfileRecord PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ProfileRecord>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeProfileRecord(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ProfileRecord)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ProfileRecord>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ProfileRecord)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ProfileRecord>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ProfileRecord IPersistableModel<ProfileRecord>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ProfileRecord>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator ProfileRecord(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeProfileRecord(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<ProfileRecord>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ProfileRecord>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ProfileRecord)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("profileId"u8);
            writer.WriteStringValue(ProfileId);
            if (Optional.IsDefined(TraceId))
            {
                writer.WritePropertyName("traceId"u8);
                writer.WriteStringValue(TraceId);
            }
            if (Optional.IsDefined(SpanId))
            {
                writer.WritePropertyName("spanId"u8);
                writer.WriteStringValue(SpanId);
            }
            if (Optional.IsDefined(SessionId))
            {
                writer.WritePropertyName("sessionId"u8);
                writer.WriteStringValue(SessionId);
            }
            writer.WritePropertyName("timeUnixNano"u8);
            writer.WriteNumberValue(TimeUnixNano);
            writer.WritePropertyName("durationNano"u8);
            writer.WriteNumberValue(DurationNano);
            writer.WritePropertyName("sampleCount"u8);
            writer.WriteNumberValue(SampleCount);
            if (Optional.IsDefined(SampleType))
            {
                writer.WritePropertyName("sampleType"u8);
                writer.WriteStringValue(SampleType);
            }
            if (Optional.IsDefined(SampleUnit))
            {
                writer.WritePropertyName("sampleUnit"u8);
                writer.WriteStringValue(SampleUnit);
            }
            if (Optional.IsDefined(OriginalPayloadFormat))
            {
                writer.WritePropertyName("originalPayloadFormat"u8);
                writer.WriteStringValue(OriginalPayloadFormat);
            }
            if (Optional.IsDefined(ServiceName))
            {
                writer.WritePropertyName("serviceName"u8);
                writer.WriteStringValue(ServiceName);
            }
            if (Optional.IsDefined(ProfileFrameType))
            {
                writer.WritePropertyName("profileFrameType"u8);
                writer.WriteStringValue(ProfileFrameType);
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
            if (Optional.IsDefined(ProfileDataJson))
            {
                writer.WritePropertyName("profileDataJson"u8);
                writer.WriteStringValue(ProfileDataJson);
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

        ProfileRecord IJsonModel<ProfileRecord>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ProfileRecord JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ProfileRecord>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ProfileRecord)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeProfileRecord(document.RootElement, options);
        }

        internal static ProfileRecord DeserializeProfileRecord(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string profileId = default;
            string traceId = default;
            string spanId = default;
            string sessionId = default;
            long timeUnixNano = default;
            long durationNano = default;
            int sampleCount = default;
            string sampleType = default;
            string sampleUnit = default;
            string originalPayloadFormat = default;
            string serviceName = default;
            string profileFrameType = default;
            string attributesJson = default;
            string resourceJson = default;
            string profileDataJson = default;
            string schemaUrl = default;
            DateTimeOffset? createdAt = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("profileId"u8))
                {
                    profileId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("traceId"u8))
                {
                    traceId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("spanId"u8))
                {
                    spanId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("sessionId"u8))
                {
                    sessionId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("timeUnixNano"u8))
                {
                    timeUnixNano = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("durationNano"u8))
                {
                    durationNano = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("sampleCount"u8))
                {
                    sampleCount = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("sampleType"u8))
                {
                    sampleType = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("sampleUnit"u8))
                {
                    sampleUnit = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("originalPayloadFormat"u8))
                {
                    originalPayloadFormat = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("serviceName"u8))
                {
                    serviceName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("profileFrameType"u8))
                {
                    profileFrameType = prop.Value.GetString();
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
                if (prop.NameEquals("profileDataJson"u8))
                {
                    profileDataJson = prop.Value.GetString();
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
            return new ProfileRecord(
                profileId,
                traceId,
                spanId,
                sessionId,
                timeUnixNano,
                durationNano,
                sampleCount,
                sampleType,
                sampleUnit,
                originalPayloadFormat,
                serviceName,
                profileFrameType,
                attributesJson,
                resourceJson,
                profileDataJson,
                schemaUrl,
                createdAt,
                additionalBinaryDataProperties);
        }
    }
}
