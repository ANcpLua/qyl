
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Domains.Observe.Log;

namespace Qyl.Api
{
    public partial class LogAggregationRequest : IJsonModel<LogAggregationRequest>
    {
        internal LogAggregationRequest()
        {
        }

        protected virtual LogAggregationRequest PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogAggregationRequest(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogAggregationRequest)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogAggregationRequest)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogAggregationRequest>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogAggregationRequest IPersistableModel<LogAggregationRequest>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogAggregationRequest>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(LogAggregationRequest logAggregationRequest)
        {
            if (logAggregationRequest == null)
            {
                return null;
            }
            return BinaryContent.Create(logAggregationRequest, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<LogAggregationRequest>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogAggregationRequest)} does not support writing '{format}' format.");
            }
            if (Optional.IsDefined(Query))
            {
                writer.WritePropertyName("query"u8);
                writer.WriteObjectValue(Query, options);
            }
            writer.WritePropertyName("aggregation"u8);
            writer.WriteObjectValue(Aggregation, options);
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

        LogAggregationRequest IJsonModel<LogAggregationRequest>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogAggregationRequest JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogAggregationRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogAggregationRequest)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogAggregationRequest(document.RootElement, options);
        }

        internal static LogAggregationRequest DeserializeLogAggregationRequest(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            LogQuery query = default;
            LogAggregation aggregation = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("query"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    query = LogQuery.DeserializeLogQuery(prop.Value, options);
                    continue;
                }
                if (prop.NameEquals("aggregation"u8))
                {
                    aggregation = LogAggregation.DeserializeLogAggregation(prop.Value, options);
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogAggregationRequest(query, aggregation, additionalBinaryDataProperties);
        }
    }
}
