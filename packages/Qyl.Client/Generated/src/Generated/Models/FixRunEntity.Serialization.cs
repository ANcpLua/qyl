
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Alerting
{
    public partial class FixRunEntity : IJsonModel<FixRunEntity>
    {
        internal FixRunEntity()
        {
        }

        protected virtual FixRunEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<FixRunEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeFixRunEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(FixRunEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<FixRunEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(FixRunEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<FixRunEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        FixRunEntity IPersistableModel<FixRunEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<FixRunEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator FixRunEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeFixRunEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<FixRunEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<FixRunEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(FixRunEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("issue_id"u8);
            writer.WriteStringValue(IssueId);
            if (Optional.IsDefined(AlertFiringId))
            {
                writer.WritePropertyName("alert_firing_id"u8);
                writer.WriteStringValue(AlertFiringId);
            }
            writer.WritePropertyName("trigger_type"u8);
            writer.WriteStringValue(TriggerType.ToSerialString());
            writer.WritePropertyName("strategy"u8);
            writer.WriteStringValue(Strategy);
            if (Optional.IsDefined(ModelName))
            {
                writer.WritePropertyName("model_name"u8);
                writer.WriteStringValue(ModelName);
            }
            if (Optional.IsDefined(ModelProvider))
            {
                writer.WritePropertyName("model_provider"u8);
                writer.WriteStringValue(ModelProvider);
            }
            writer.WritePropertyName("status"u8);
            writer.WriteStringValue(Status.ToSerialString());
            if (Optional.IsDefined(ErrorMessage))
            {
                writer.WritePropertyName("error_message"u8);
                writer.WriteStringValue(ErrorMessage);
            }
            if (Optional.IsDefined(TokensUsed))
            {
                writer.WritePropertyName("tokens_used"u8);
                writer.WriteNumberValue(TokensUsed.Value);
            }
            if (Optional.IsDefined(DurationMs))
            {
                writer.WritePropertyName("duration_ms"u8);
                writer.WriteNumberValue(DurationMs.Value);
            }
            writer.WritePropertyName("created_at"u8);
            writer.WriteStringValue(CreatedAt, "O");
            if (Optional.IsDefined(StartedAt))
            {
                writer.WritePropertyName("started_at"u8);
                writer.WriteStringValue(StartedAt.Value, "O");
            }
            if (Optional.IsDefined(CompletedAt))
            {
                writer.WritePropertyName("completed_at"u8);
                writer.WriteStringValue(CompletedAt.Value, "O");
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

        FixRunEntity IJsonModel<FixRunEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual FixRunEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<FixRunEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(FixRunEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeFixRunEntity(document.RootElement, options);
        }

        internal static FixRunEntity DeserializeFixRunEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string issueId = default;
            string alertFiringId = default;
            FixTriggerType triggerType = default;
            string strategy = default;
            string modelName = default;
            string modelProvider = default;
            FixRunStatus status = default;
            string errorMessage = default;
            int? tokensUsed = default;
            int? durationMs = default;
            DateTimeOffset createdAt = default;
            DateTimeOffset? startedAt = default;
            DateTimeOffset? completedAt = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("id"u8))
                {
                    id = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("issue_id"u8))
                {
                    issueId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("alert_firing_id"u8))
                {
                    alertFiringId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("trigger_type"u8))
                {
                    triggerType = prop.Value.GetString().ToFixTriggerType();
                    continue;
                }
                if (prop.NameEquals("strategy"u8))
                {
                    strategy = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("model_name"u8))
                {
                    modelName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("model_provider"u8))
                {
                    modelProvider = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("status"u8))
                {
                    status = prop.Value.GetString().ToFixRunStatus();
                    continue;
                }
                if (prop.NameEquals("error_message"u8))
                {
                    errorMessage = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("tokens_used"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    tokensUsed = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("duration_ms"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    durationMs = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("created_at"u8))
                {
                    createdAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("started_at"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    startedAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("completed_at"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    completedAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new FixRunEntity(
                id,
                issueId,
                alertFiringId,
                triggerType,
                strategy,
                modelName,
                modelProvider,
                status,
                errorMessage,
                tokensUsed,
                durationMs,
                createdAt,
                startedAt,
                completedAt,
                additionalBinaryDataProperties);
        }
    }
}
