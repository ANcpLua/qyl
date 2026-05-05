
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Domains.Configurator;

namespace Qyl.Api
{
    public partial class GenerationJobCreateRequest : IJsonModel<GenerationJobCreateRequest>
    {
        internal GenerationJobCreateRequest()
        {
        }

        protected virtual GenerationJobCreateRequest PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationJobCreateRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeGenerationJobCreateRequest(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(GenerationJobCreateRequest)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationJobCreateRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(GenerationJobCreateRequest)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<GenerationJobCreateRequest>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        GenerationJobCreateRequest IPersistableModel<GenerationJobCreateRequest>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<GenerationJobCreateRequest>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(GenerationJobCreateRequest generationJobCreateRequest)
        {
            if (generationJobCreateRequest == null)
            {
                return null;
            }
            return BinaryContent.Create(generationJobCreateRequest, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<GenerationJobCreateRequest>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationJobCreateRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(GenerationJobCreateRequest)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("workspace_id"u8);
            writer.WriteStringValue(WorkspaceId);
            writer.WritePropertyName("profile_id"u8);
            writer.WriteStringValue(ProfileId);
            writer.WritePropertyName("job_type"u8);
            writer.WriteStringValue(JobType.ToSerialString());
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

        GenerationJobCreateRequest IJsonModel<GenerationJobCreateRequest>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual GenerationJobCreateRequest JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<GenerationJobCreateRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(GenerationJobCreateRequest)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeGenerationJobCreateRequest(document.RootElement, options);
        }

        internal static GenerationJobCreateRequest DeserializeGenerationJobCreateRequest(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string workspaceId = default;
            string profileId = default;
            GenerationJobType jobType = default;
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
                if (prop.NameEquals("job_type"u8))
                {
                    jobType = prop.Value.GetString().ToGenerationJobType();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new GenerationJobCreateRequest(workspaceId, profileId, jobType, additionalBinaryDataProperties);
        }
    }
}
