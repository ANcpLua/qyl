
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Domains.Workspace;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageProjectEntity : IJsonModel<CursorPageProjectEntity>
    {
        internal CursorPageProjectEntity()
        {
        }

        protected virtual CursorPageProjectEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageProjectEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeCursorPageProjectEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(CursorPageProjectEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageProjectEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(CursorPageProjectEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<CursorPageProjectEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        CursorPageProjectEntity IPersistableModel<CursorPageProjectEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<CursorPageProjectEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator CursorPageProjectEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeCursorPageProjectEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<CursorPageProjectEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageProjectEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(CursorPageProjectEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("items"u8);
            writer.WriteStartArray();
            foreach (ProjectEntity item in Items)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            if (Optional.IsDefined(NextCursor))
            {
                writer.WritePropertyName("next_cursor"u8);
                writer.WriteStringValue(NextCursor);
            }
            if (Optional.IsDefined(PrevCursor))
            {
                writer.WritePropertyName("prev_cursor"u8);
                writer.WriteStringValue(PrevCursor);
            }
            writer.WritePropertyName("has_more"u8);
            writer.WriteBooleanValue(HasMore);
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

        CursorPageProjectEntity IJsonModel<CursorPageProjectEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual CursorPageProjectEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageProjectEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(CursorPageProjectEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeCursorPageProjectEntity(document.RootElement, options);
        }

        internal static CursorPageProjectEntity DeserializeCursorPageProjectEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            IList<ProjectEntity> items = default;
            string nextCursor = default;
            string prevCursor = default;
            bool hasMore = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("items"u8))
                {
                    List<ProjectEntity> array = new List<ProjectEntity>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(ProjectEntity.DeserializeProjectEntity(item, options));
                    }
                    items = array;
                    continue;
                }
                if (prop.NameEquals("next_cursor"u8))
                {
                    nextCursor = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("prev_cursor"u8))
                {
                    prevCursor = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("has_more"u8))
                {
                    hasMore = prop.Value.GetBoolean();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new CursorPageProjectEntity(items, nextCursor, prevCursor, hasMore, additionalBinaryDataProperties);
        }
    }
}
