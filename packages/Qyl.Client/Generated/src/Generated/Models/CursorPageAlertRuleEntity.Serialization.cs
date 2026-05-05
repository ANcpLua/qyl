
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Domains.Alerting;

namespace Qyl.Common.Pagination
{
    public partial class CursorPageAlertRuleEntity : IJsonModel<CursorPageAlertRuleEntity>
    {
        internal CursorPageAlertRuleEntity()
        {
        }

        protected virtual CursorPageAlertRuleEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageAlertRuleEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeCursorPageAlertRuleEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(CursorPageAlertRuleEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageAlertRuleEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(CursorPageAlertRuleEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<CursorPageAlertRuleEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        CursorPageAlertRuleEntity IPersistableModel<CursorPageAlertRuleEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<CursorPageAlertRuleEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator CursorPageAlertRuleEntity(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeCursorPageAlertRuleEntity(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<CursorPageAlertRuleEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageAlertRuleEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(CursorPageAlertRuleEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("items"u8);
            writer.WriteStartArray();
            foreach (AlertRuleEntity item in Items)
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

        CursorPageAlertRuleEntity IJsonModel<CursorPageAlertRuleEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual CursorPageAlertRuleEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CursorPageAlertRuleEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(CursorPageAlertRuleEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeCursorPageAlertRuleEntity(document.RootElement, options);
        }

        internal static CursorPageAlertRuleEntity DeserializeCursorPageAlertRuleEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            IList<AlertRuleEntity> items = default;
            string nextCursor = default;
            string prevCursor = default;
            bool hasMore = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("items"u8))
                {
                    List<AlertRuleEntity> array = new List<AlertRuleEntity>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(AlertRuleEntity.DeserializeAlertRuleEntity(item, options));
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
            return new CursorPageAlertRuleEntity(items, nextCursor, prevCursor, hasMore, additionalBinaryDataProperties);
        }
    }
}
