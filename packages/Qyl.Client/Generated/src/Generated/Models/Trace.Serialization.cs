
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.OTel.Traces
{
    public partial class Trace : IJsonModel<Trace>
    {
        internal Trace()
        {
        }

        protected virtual Trace PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<Trace>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeTrace(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(Trace)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<Trace>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(Trace)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<Trace>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        Trace IPersistableModel<Trace>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<Trace>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator Trace(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeTrace(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<Trace>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<Trace>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(Trace)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("trace_id"u8);
            writer.WriteStringValue(TraceId);
            writer.WritePropertyName("spans"u8);
            writer.WriteStartArray();
            foreach (Span item in Spans)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            if (Optional.IsDefined(RootSpan))
            {
                writer.WritePropertyName("root_span"u8);
                writer.WriteObjectValue(RootSpan, options);
            }
            writer.WritePropertyName("span_count"u8);
            writer.WriteNumberValue(SpanCount);
            writer.WritePropertyName("duration_ns"u8);
            writer.WriteNumberValue(DurationNs);
            writer.WritePropertyName("start_time"u8);
            writer.WriteStringValue(StartTime, "O");
            writer.WritePropertyName("end_time"u8);
            writer.WriteStringValue(EndTime, "O");
            writer.WritePropertyName("services"u8);
            writer.WriteStartArray();
            foreach (string item in Services)
            {
                if (item == null)
                {
                    writer.WriteNullValue();
                    continue;
                }
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("has_error"u8);
            writer.WriteBooleanValue(HasError);
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

        Trace IJsonModel<Trace>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual Trace JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<Trace>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(Trace)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeTrace(document.RootElement, options);
        }

        internal static Trace DeserializeTrace(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string traceId = default;
            IList<Span> spans = default;
            Span rootSpan = default;
            int spanCount = default;
            long durationNs = default;
            DateTimeOffset startTime = default;
            DateTimeOffset endTime = default;
            IList<string> services = default;
            bool hasError = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("trace_id"u8))
                {
                    traceId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("spans"u8))
                {
                    List<Span> array = new List<Span>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(Span.DeserializeSpan(item, options));
                    }
                    spans = array;
                    continue;
                }
                if (prop.NameEquals("root_span"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    rootSpan = Span.DeserializeSpan(prop.Value, options);
                    continue;
                }
                if (prop.NameEquals("span_count"u8))
                {
                    spanCount = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("duration_ns"u8))
                {
                    durationNs = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("start_time"u8))
                {
                    startTime = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("end_time"u8))
                {
                    endTime = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (prop.NameEquals("services"u8))
                {
                    List<string> array = new List<string>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Null)
                        {
                            array.Add(null);
                        }
                        else
                        {
                            array.Add(item.GetString());
                        }
                    }
                    services = array;
                    continue;
                }
                if (prop.NameEquals("has_error"u8))
                {
                    hasError = prop.Value.GetBoolean();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new Trace(
                traceId,
                spans,
                rootSpan,
                spanCount,
                durationNs,
                startTime,
                endTime,
                services,
                hasError,
                additionalBinaryDataProperties);
        }
    }
}
