
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Common;

namespace Qyl.OTel.Traces
{
    public partial class SpanLink : IJsonModel<SpanLink>
    {
        internal SpanLink()
        {
        }

        protected virtual SpanLink PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanLink>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSpanLink(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SpanLink)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanLink>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SpanLink)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SpanLink>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SpanLink IPersistableModel<SpanLink>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SpanLink>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<SpanLink>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanLink>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SpanLink)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("trace_id"u8);
            writer.WriteStringValue(TraceId);
            writer.WritePropertyName("span_id"u8);
            writer.WriteStringValue(SpanId);
            if (Optional.IsDefined(TraceState))
            {
                writer.WritePropertyName("trace_state"u8);
                writer.WriteStringValue(TraceState);
            }
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
            if (Optional.IsDefined(Flags))
            {
                writer.WritePropertyName("flags"u8);
                writer.WriteNumberValue(Flags.Value);
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

        SpanLink IJsonModel<SpanLink>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SpanLink JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SpanLink>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SpanLink)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSpanLink(document.RootElement, options);
        }

        internal static SpanLink DeserializeSpanLink(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string traceId = default;
            string spanId = default;
            string traceState = default;
            IList<Common.Attribute> attributes = default;
            long? droppedAttributesCount = default;
            int? flags = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("trace_id"u8))
                {
                    traceId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("span_id"u8))
                {
                    spanId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("trace_state"u8))
                {
                    traceState = prop.Value.GetString();
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
                if (prop.NameEquals("flags"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    flags = prop.Value.GetInt32();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SpanLink(
                traceId,
                spanId,
                traceState,
                attributes ?? new ChangeTrackingList<Common.Attribute>(),
                droppedAttributesCount,
                flags,
                additionalBinaryDataProperties);
        }
    }
}
