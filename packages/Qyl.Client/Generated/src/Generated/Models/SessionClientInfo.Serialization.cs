
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionClientInfo : IJsonModel<SessionClientInfo>
    {
        protected virtual SessionClientInfo PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionClientInfo>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSessionClientInfo(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SessionClientInfo)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionClientInfo>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SessionClientInfo)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SessionClientInfo>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SessionClientInfo IPersistableModel<SessionClientInfo>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SessionClientInfo>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<SessionClientInfo>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionClientInfo>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SessionClientInfo)} does not support writing '{format}' format.");
            }
            if (Optional.IsDefined(Ip))
            {
                writer.WritePropertyName("ip"u8);
                writer.WriteStringValue(Ip);
            }
            if (Optional.IsDefined(UserAgent))
            {
                writer.WritePropertyName("user_agent"u8);
                writer.WriteStringValue(UserAgent);
            }
            if (Optional.IsDefined(DeviceType))
            {
                writer.WritePropertyName("device_type"u8);
                writer.WriteStringValue(DeviceType.Value.ToSerialString());
            }
            if (Optional.IsDefined(Os))
            {
                writer.WritePropertyName("os"u8);
                writer.WriteStringValue(Os);
            }
            if (Optional.IsDefined(Browser))
            {
                writer.WritePropertyName("browser"u8);
                writer.WriteStringValue(Browser);
            }
            if (Optional.IsDefined(BrowserVersion))
            {
                writer.WritePropertyName("browser_version"u8);
                writer.WriteStringValue(BrowserVersion);
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

        SessionClientInfo IJsonModel<SessionClientInfo>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SessionClientInfo JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionClientInfo>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SessionClientInfo)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSessionClientInfo(document.RootElement, options);
        }

        internal static SessionClientInfo DeserializeSessionClientInfo(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string ip = default;
            string userAgent = default;
            DeviceType? deviceType = default;
            string os = default;
            string browser = default;
            string browserVersion = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("ip"u8))
                {
                    ip = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("user_agent"u8))
                {
                    userAgent = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("device_type"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    deviceType = prop.Value.GetString().ToDeviceType();
                    continue;
                }
                if (prop.NameEquals("os"u8))
                {
                    os = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("browser"u8))
                {
                    browser = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("browser_version"u8))
                {
                    browserVersion = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SessionClientInfo(
                ip,
                userAgent,
                deviceType,
                os,
                browser,
                browserVersion,
                additionalBinaryDataProperties);
        }
    }
}
