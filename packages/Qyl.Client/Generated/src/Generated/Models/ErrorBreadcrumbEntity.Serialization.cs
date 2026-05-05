
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Issues
{
    public partial class ErrorBreadcrumbEntity : IJsonModel<ErrorBreadcrumbEntity>
    {
        internal ErrorBreadcrumbEntity()
        {
        }

        protected virtual ErrorBreadcrumbEntity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorBreadcrumbEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeErrorBreadcrumbEntity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ErrorBreadcrumbEntity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorBreadcrumbEntity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ErrorBreadcrumbEntity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ErrorBreadcrumbEntity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ErrorBreadcrumbEntity IPersistableModel<ErrorBreadcrumbEntity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ErrorBreadcrumbEntity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<ErrorBreadcrumbEntity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorBreadcrumbEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorBreadcrumbEntity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("id"u8);
            writer.WriteStringValue(Id);
            writer.WritePropertyName("event_id"u8);
            writer.WriteStringValue(EventId);
            writer.WritePropertyName("breadcrumb_type"u8);
            writer.WriteStringValue(BreadcrumbType.ToSerialString());
            if (Optional.IsDefined(Category))
            {
                writer.WritePropertyName("category"u8);
                writer.WriteStringValue(Category);
            }
            if (Optional.IsDefined(Message))
            {
                writer.WritePropertyName("message"u8);
                writer.WriteStringValue(Message);
            }
            writer.WritePropertyName("level"u8);
            writer.WriteStringValue(Level);
            if (Optional.IsDefined(DataJson))
            {
                writer.WritePropertyName("data_json"u8);
                writer.WriteStringValue(DataJson);
            }
            writer.WritePropertyName("timestamp"u8);
            writer.WriteStringValue(Timestamp, "O");
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

        ErrorBreadcrumbEntity IJsonModel<ErrorBreadcrumbEntity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ErrorBreadcrumbEntity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorBreadcrumbEntity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorBreadcrumbEntity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeErrorBreadcrumbEntity(document.RootElement, options);
        }

        internal static ErrorBreadcrumbEntity DeserializeErrorBreadcrumbEntity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string id = default;
            string eventId = default;
            BreadcrumbType breadcrumbType = default;
            string category = default;
            string message = default;
            string level = default;
            string dataJson = default;
            DateTimeOffset timestamp = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("id"u8))
                {
                    id = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("event_id"u8))
                {
                    eventId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("breadcrumb_type"u8))
                {
                    breadcrumbType = prop.Value.GetString().ToBreadcrumbType();
                    continue;
                }
                if (prop.NameEquals("category"u8))
                {
                    category = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("message"u8))
                {
                    message = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("level"u8))
                {
                    level = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("data_json"u8))
                {
                    dataJson = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("timestamp"u8))
                {
                    timestamp = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ErrorBreadcrumbEntity(
                id,
                eventId,
                breadcrumbType,
                category,
                message,
                level,
                dataJson,
                timestamp,
                additionalBinaryDataProperties);
        }
    }
}
