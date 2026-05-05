
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class ServiceInfo : IJsonModel<ServiceInfo>
    {
        internal ServiceInfo()
        {
        }

        protected virtual ServiceInfo PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ServiceInfo>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeServiceInfo(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ServiceInfo)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ServiceInfo>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ServiceInfo)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ServiceInfo>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ServiceInfo IPersistableModel<ServiceInfo>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ServiceInfo>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<ServiceInfo>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ServiceInfo>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ServiceInfo)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("name"u8);
            writer.WriteStringValue(Name);
            if (Optional.IsDefined(NamespaceName))
            {
                writer.WritePropertyName("namespace_name"u8);
                writer.WriteStringValue(NamespaceName);
            }
            if (Optional.IsDefined(Version))
            {
                writer.WritePropertyName("version"u8);
                writer.WriteStringValue(Version);
            }
            writer.WritePropertyName("instance_count"u8);
            writer.WriteNumberValue(InstanceCount);
            writer.WritePropertyName("last_seen"u8);
            writer.WriteStringValue(LastSeen, "O");
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

        ServiceInfo IJsonModel<ServiceInfo>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ServiceInfo JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ServiceInfo>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ServiceInfo)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeServiceInfo(document.RootElement, options);
        }

        internal static ServiceInfo DeserializeServiceInfo(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string name = default;
            string namespaceName = default;
            string version = default;
            int instanceCount = default;
            DateTimeOffset lastSeen = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("name"u8))
                {
                    name = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("namespace_name"u8))
                {
                    namespaceName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("version"u8))
                {
                    version = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("instance_count"u8))
                {
                    instanceCount = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("last_seen"u8))
                {
                    lastSeen = prop.Value.GetDateTimeOffset("O");
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ServiceInfo(
                name,
                namespaceName,
                version,
                instanceCount,
                lastSeen,
                additionalBinaryDataProperties);
        }
    }
}
