
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.OTel.Logs
{
    public partial class LogCountByDimension : IJsonModel<LogCountByDimension>
    {
        internal LogCountByDimension()
        {
        }

        protected virtual LogCountByDimension PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogCountByDimension>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogCountByDimension(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogCountByDimension)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogCountByDimension>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogCountByDimension)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogCountByDimension>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogCountByDimension IPersistableModel<LogCountByDimension>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogCountByDimension>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<LogCountByDimension>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogCountByDimension>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogCountByDimension)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("dimension"u8);
            writer.WriteStringValue(Dimension);
            writer.WritePropertyName("count"u8);
            writer.WriteNumberValue(Count);
            writer.WritePropertyName("error_count"u8);
            writer.WriteNumberValue(ErrorCount);
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

        LogCountByDimension IJsonModel<LogCountByDimension>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogCountByDimension JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogCountByDimension>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogCountByDimension)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogCountByDimension(document.RootElement, options);
        }

        internal static LogCountByDimension DeserializeLogCountByDimension(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string dimension = default;
            long count = default;
            long errorCount = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("dimension"u8))
                {
                    dimension = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("count"u8))
                {
                    count = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("error_count"u8))
                {
                    errorCount = prop.Value.GetInt64();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogCountByDimension(dimension, count, errorCount, additionalBinaryDataProperties);
        }
    }
}
