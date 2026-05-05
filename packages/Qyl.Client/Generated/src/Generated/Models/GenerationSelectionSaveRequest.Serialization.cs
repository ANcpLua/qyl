
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class GenerationSelectionSaveRequest : IJsonModel<GenerationSelectionSaveRequest>
    {
        internal GenerationSelectionSaveRequest()
        {
        }

        protected virtual GenerationSelectionSaveRequest PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationSelectionSaveRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeGenerationSelectionSaveRequest(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(GenerationSelectionSaveRequest)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationSelectionSaveRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(GenerationSelectionSaveRequest)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<GenerationSelectionSaveRequest>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        GenerationSelectionSaveRequest IPersistableModel<GenerationSelectionSaveRequest>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<GenerationSelectionSaveRequest>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(GenerationSelectionSaveRequest generationSelectionSaveRequest)
        {
            if (generationSelectionSaveRequest == null)
            {
                return null;
            }
            return BinaryContent.Create(generationSelectionSaveRequest, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<GenerationSelectionSaveRequest>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationSelectionSaveRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(GenerationSelectionSaveRequest)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("workspace_id"u8);
            writer.WriteStringValue(WorkspaceId);
            writer.WritePropertyName("profile_id"u8);
            writer.WriteStringValue(ProfileId);
            writer.WritePropertyName("selected_keys_json"u8);
            writer.WriteStringValue(SelectedKeysJson);
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

        GenerationSelectionSaveRequest IJsonModel<GenerationSelectionSaveRequest>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual GenerationSelectionSaveRequest JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationSelectionSaveRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(GenerationSelectionSaveRequest)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeGenerationSelectionSaveRequest(document.RootElement, options);
        }

        internal static GenerationSelectionSaveRequest DeserializeGenerationSelectionSaveRequest(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string workspaceId = default;
            string profileId = default;
            string selectedKeysJson = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
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
                if (prop.NameEquals("selected_keys_json"u8))
                {
                    selectedKeysJson = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new GenerationSelectionSaveRequest(workspaceId, profileId, selectedKeysJson, additionalBinaryDataProperties);
        }
    }
}
