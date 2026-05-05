
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Common
{
    public partial class InstrumentationScope : IJsonModel<InstrumentationScope>
    {
        internal InstrumentationScope()
        {
        }

        protected virtual InstrumentationScope PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<InstrumentationScope>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeInstrumentationScope(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(InstrumentationScope)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<InstrumentationScope>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(InstrumentationScope)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<InstrumentationScope>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        InstrumentationScope IPersistableModel<InstrumentationScope>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<InstrumentationScope>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<InstrumentationScope>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<InstrumentationScope>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(InstrumentationScope)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("name"u8);
            writer.WriteStringValue(ScopeName);
            if (Optional.IsDefined(ScopeVersion))
            {
                writer.WritePropertyName("version"u8);
                writer.WriteStringValue(ScopeVersion);
            }
            if (Optional.IsCollectionDefined(ScopeAttributes))
            {
                writer.WritePropertyName("attributes"u8);
                writer.WriteStartArray();
                foreach (Attribute item in ScopeAttributes)
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

        InstrumentationScope IJsonModel<InstrumentationScope>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual InstrumentationScope JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<InstrumentationScope>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(InstrumentationScope)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeInstrumentationScope(document.RootElement, options);
        }

        internal static InstrumentationScope DeserializeInstrumentationScope(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string scopeName = default;
            string scopeVersion = default;
            IList<Attribute> scopeAttributes = default;
            long? droppedAttributesCount = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("name"u8))
                {
                    scopeName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("version"u8))
                {
                    scopeVersion = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("attributes"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<Attribute> array = new List<Attribute>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(Attribute.DeserializeAttribute(item, options));
                    }
                    scopeAttributes = array;
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
            return new InstrumentationScope(scopeName, scopeVersion, scopeAttributes ?? new ChangeTrackingList<Attribute>(), droppedAttributesCount, additionalBinaryDataProperties);
        }
    }
}
