
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Domains.Issues;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageErrorIssueEntity : IJsonModel<CursorPageErrorIssueEntity>
    {
        internal CursorPageErrorIssueEntity()
        {
        }

        protected virtual CursorPageErrorIssueEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageErrorIssueEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeCursorPageErrorIssueEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(CursorPageErrorIssueEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageErrorIssueEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(CursorPageErrorIssueEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<CursorPageErrorIssueEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        CursorPageErrorIssueEntity IPersistableModel<CursorPageErrorIssueEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<CursorPageErrorIssueEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator CursorPageErrorIssueEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeCursorPageErrorIssueEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<CursorPageErrorIssueEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageErrorIssueEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(CursorPageErrorIssueEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("items"u8);
            writer.WriteStartArray();
            foreach (ErrorIssueEntity item in Items)
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

        CursorPageErrorIssueEntity IJsonModel<CursorPageErrorIssueEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual CursorPageErrorIssueEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageErrorIssueEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(CursorPageErrorIssueEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeCursorPageErrorIssueEntity(document.RootElement, options);
        }

        internal static CursorPageErrorIssueEntity DeserializeCursorPageErrorIssueEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            IList<ErrorIssueEntity> items = default;
            string nextCursor = default;
            string prevCursor = default;
            bool hasMore = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("items"u8))
                {
                    List<ErrorIssueEntity> array = new List<ErrorIssueEntity>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(ErrorIssueEntity.DeserializeErrorIssueEntity(item, options));
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
            return new CursorPageErrorIssueEntity(items, nextCursor, prevCursor, hasMore, additionalBinaryDataProperties);
        }
    }
}
