
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorCategoryStats : IJsonModel<ErrorCategoryStats>
    {
        internal ErrorCategoryStats()
        {
        }

        protected virtual ErrorCategoryStats PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorCategoryStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeErrorCategoryStats(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ErrorCategoryStats)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorCategoryStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ErrorCategoryStats)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ErrorCategoryStats>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ErrorCategoryStats IPersistableModel<ErrorCategoryStats>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ErrorCategoryStats>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<ErrorCategoryStats>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorCategoryStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorCategoryStats)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("category"u8);
            writer.WriteStringValue(Category.ToSerialString());
            writer.WritePropertyName("count"u8);
            writer.WriteNumberValue(Count);
            writer.WritePropertyName("percentage"u8);
            writer.WriteNumberValue(Percentage);
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

        ErrorCategoryStats IJsonModel<ErrorCategoryStats>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ErrorCategoryStats JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorCategoryStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorCategoryStats)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeErrorCategoryStats(document.RootElement, options);
        }

        internal static ErrorCategoryStats DeserializeErrorCategoryStats(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            ErrorCategory category = default;
            long count = default;
            double percentage = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("category"u8))
                {
                    category = prop.Value.GetString().ToErrorCategory();
                    continue;
                }
                if (prop.NameEquals("count"u8))
                {
                    count = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("percentage"u8))
                {
                    percentage = prop.Value.GetDouble();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ErrorCategoryStats(category, count, percentage, additionalBinaryDataProperties);
        }
    }
}
