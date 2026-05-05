
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Workspace
{
    public partial class ProjectEnvironmentEntity : IJsonModel<ProjectEnvironmentEntity>
    {
        internal ProjectEnvironmentEntity()
        {
        }

        protected virtual ProjectEnvironmentEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ProjectEnvironmentEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeProjectEnvironmentEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ProjectEnvironmentEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ProjectEnvironmentEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ProjectEnvironmentEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ProjectEnvironmentEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ProjectEnvironmentEntity IPersistableModel<ProjectEnvironmentEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ProjectEnvironmentEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<ProjectEnvironmentEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ProjectEnvironmentEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ProjectEnvironmentEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("project_id"u8);
            writer.WriteStringValue(ProjectId);
            writer.WritePropertyName("name"u8);
            writer.WriteStringValue(Name);
            writer.WritePropertyName("display_name"u8);
            writer.WriteStringValue(DisplayName);
            if (Optional.IsDefined(Color))
            {
                writer.WritePropertyName("color"u8);
                writer.WriteStringValue(Color);
            }
            writer.WritePropertyName("sort_order"u8);
            writer.WriteNumberValue(SortOrder);
            writer.WritePropertyName("created_at"u8);
            writer.WriteStringValue(CreatedAt, "O");
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

        ProjectEnvironmentEntity IJsonModel<ProjectEnvironmentEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ProjectEnvironmentEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ProjectEnvironmentEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ProjectEnvironmentEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeProjectEnvironmentEntity(document.RootElement, options);
        }

        internal static ProjectEnvironmentEntity DeserializeProjectEnvironmentEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string projectId = default;
            string name = default;
            string displayName = default;
            string color = default;
            int sortOrder = default;
            DateTimeOffset createdAt = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("id"u8))
                {
                    id = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("project_id"u8))
                {
                    projectId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("name"u8))
                {
                    name = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("display_name"u8))
                {
                    displayName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("color"u8))
                {
                    color = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("sort_order"u8))
                {
                    sortOrder = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("created_at"u8))
                {
                    createdAt = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ProjectEnvironmentEntity(
                id,
                projectId,
                name,
                displayName,
                color,
                sortOrder,
                createdAt,
                additionalBinaryDataProperties);
        }
    }
}
