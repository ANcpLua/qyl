
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class LogAggregationBucket : IJsonModel<LogAggregationBucket>
    {
        internal LogAggregationBucket()
        {
        }

        protected virtual LogAggregationBucket PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationBucket>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogAggregationBucket(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogAggregationBucket)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationBucket>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogAggregationBucket)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogAggregationBucket>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogAggregationBucket IPersistableModel<LogAggregationBucket>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogAggregationBucket>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<LogAggregationBucket>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationBucket>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogAggregationBucket)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("key"u8);
            writer.WriteStringValue(Key);
            writer.WritePropertyName("value"u8);
            writer.WriteNumberValue(Value);
            writer.WritePropertyName("count"u8);
            writer.WriteNumberValue(Count);
            if (Optional.IsDefined(Timestamp))
            {
                writer.WritePropertyName("timestamp"u8);
                writer.WriteStringValue(Timestamp.Value, "O");
            }
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

        LogAggregationBucket IJsonModel<LogAggregationBucket>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogAggregationBucket JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationBucket>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogAggregationBucket)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogAggregationBucket(document.RootElement, options);
        }

        internal static LogAggregationBucket DeserializeLogAggregationBucket(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string key = default;
            double value = default;
            long count = default;
            DateTimeOffset? timestamp = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("key"u8))
                {
                    key = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("value"u8))
                {
                    value = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("count"u8))
                {
                    count = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("timestamp"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    timestamp = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogAggregationBucket(key, value, count, timestamp, additionalBinaryDataProperties);
        }
    }
}
