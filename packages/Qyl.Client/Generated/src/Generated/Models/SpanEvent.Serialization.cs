
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Common;

namespace Qyl.OTel.Traces
{
    public partial class SpanEvent : IJsonModel<SpanEvent>
    {
        internal SpanEvent()
        {
        }

        protected virtual SpanEvent PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanEvent>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSpanEvent(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SpanEvent)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanEvent>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SpanEvent)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SpanEvent>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SpanEvent IPersistableModel<SpanEvent>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SpanEvent>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<SpanEvent>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanEvent>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SpanEvent)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("name"u8);
            writer.WriteStringValue(Name);
            writer.WritePropertyName("time_unix_nano"u8);
            writer.WriteNumberValue(TimeUnixNano);
            if (Optional.IsCollectionDefined(Attributes))
            {
                writer.WritePropertyName("attributes"u8);
                writer.WriteStartArray();
                foreach (Common.Attribute item in Attributes)
                {
                    writer.WriteObjectValue(item, options);
                }
                writer.WriteEndArray();
            }
            if (Optional.IsDefined(DroppedAttributesCount))
            {
                writer.WritePropertyName("dropped_attributes_count"u8);
                writer.WriteNumberValue(DroppedAttributesCount.Value);
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

        SpanEvent IJsonModel<SpanEvent>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SpanEvent JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanEvent>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SpanEvent)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSpanEvent(document.RootElement, options);
        }

        internal static SpanEvent DeserializeSpanEvent(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string name = default;
            long timeUnixNano = default;
            IList<Common.Attribute> attributes = default;
            long? droppedAttributesCount = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("name"u8))
                {
                    name = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("time_unix_nano"u8))
                {
                    timeUnixNano = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("attributes"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<Common.Attribute> array = new List<Common.Attribute>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(Common.Attribute.DeserializeAttribute(item, options));
                    }
                    attributes = array;
                    continue;
                }
                if (prop.NameEquals("dropped_attributes_count"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    droppedAttributesCount = prop.Value.GetInt64();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SpanEvent(name, timeUnixNano, attributes ?? new ChangeTrackingList<Common.Attribute>(), droppedAttributesCount, additionalBinaryDataProperties);
        }
    }
}
