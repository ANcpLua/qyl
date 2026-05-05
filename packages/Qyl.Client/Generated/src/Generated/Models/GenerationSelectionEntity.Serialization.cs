
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Configurator
{
    public partial class GenerationSelectionEntity : IJsonModel<GenerationSelectionEntity>
    {
        internal GenerationSelectionEntity()
        {
        }

        protected virtual GenerationSelectionEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationSelectionEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeGenerationSelectionEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(GenerationSelectionEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationSelectionEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(GenerationSelectionEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<GenerationSelectionEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        GenerationSelectionEntity IPersistableModel<GenerationSelectionEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<GenerationSelectionEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator GenerationSelectionEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeGenerationSelectionEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<GenerationSelectionEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationSelectionEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(GenerationSelectionEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("workspace_id"u8);
            writer.WriteStringValue(WorkspaceId);
            writer.WritePropertyName("profile_id"u8);
            writer.WriteStringValue(ProfileId);
            writer.WritePropertyName("selection_type"u8);
            writer.WriteStringValue(SelectionType);
            writer.WritePropertyName("selection_key"u8);
            writer.WriteStringValue(SelectionKey);
            writer.WritePropertyName("enabled"u8);
            writer.WriteBooleanValue(Enabled);
            if (Optional.IsDefined(ConfigJson))
            {
                writer.WritePropertyName("config_json"u8);
                writer.WriteStringValue(ConfigJson);
            }
            writer.WritePropertyName("created_at"u8);
            writer.WriteStringValue(CreatedAt, "O");
            writer.WritePropertyName("updated_at"u8);
            writer.WriteStringValue(UpdatedAt, "O");
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

        GenerationSelectionEntity IJsonModel<GenerationSelectionEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual GenerationSelectionEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationSelectionEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(GenerationSelectionEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeGenerationSelectionEntity(document.RootElement, options);
        }

        internal static GenerationSelectionEntity DeserializeGenerationSelectionEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string workspaceId = default;
            string profileId = default;
            string selectionType = default;
            string selectionKey = default;
            bool enabled = default;
            string configJson = default;
            DateTimeOffset createdAt = default;
            DateTimeOffset updatedAt = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("id"u8))
                {
                    id = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("workspace_id"u8))
                {
                    workspaceId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("profile_id"u8))
                {
                    profileId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("selection_type"u8))
                {
                    selectionType = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("selection_key"u8))
                {
                    selectionKey = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("enabled"u8))
                {
                    enabled = prop.Value.GetBoolean();
                    continue;
                }
                if (prop.NameEquals("config_json"u8))
                {
                    configJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("created_at"u8))
                {
                    createdAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("updated_at"u8))
                {
                    updatedAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new GenerationSelectionEntity(
                id,
                workspaceId,
                profileId,
                selectionType,
                selectionKey,
                enabled,
                configJson,
                createdAt,
                updatedAt,
                additionalBinaryDataProperties);
        }
    }
}
