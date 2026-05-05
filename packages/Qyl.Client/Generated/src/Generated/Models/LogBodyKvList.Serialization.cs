
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.Common;

namespace Qyl.OTel.Logs
{
    public partial class LogBodyKvList : IJsonModel<LogBodyKvList>
    {
        internal LogBodyKvList()
        {
        }

        protected virtual LogBodyKvList PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogBodyKvList>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogBodyKvList(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogBodyKvList)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogBodyKvList>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogBodyKvList)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogBodyKvList>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogBodyKvList IPersistableModel<LogBodyKvList>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogBodyKvList>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<LogBodyKvList>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogBodyKvList>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogBodyKvList)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("kv_list_value"u8);
            writer.WriteStartArray();
            foreach (Common.Attribute item in KvListValue)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
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

        LogBodyKvList IJsonModel<LogBodyKvList>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogBodyKvList JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogBodyKvList>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogBodyKvList)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogBodyKvList(document.RootElement, options);
        }

        internal static LogBodyKvList DeserializeLogBodyKvList(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            IList<Common.Attribute> kvListValue = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("kv_list_value"u8))
                {
                    List<Common.Attribute> array = new List<Common.Attribute>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(Common.Attribute.DeserializeAttribute(item, options));
                    }
                    kvListValue = array;
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogBodyKvList(kvListValue, additionalBinaryDataProperties);
        }
    }
}
