
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionGeoInfo : IJsonModel<SessionGeoInfo>
    {
        protected virtual SessionGeoInfo PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionGeoInfo>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSessionGeoInfo(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SessionGeoInfo)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionGeoInfo>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SessionGeoInfo)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SessionGeoInfo>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SessionGeoInfo IPersistableModel<SessionGeoInfo>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SessionGeoInfo>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<SessionGeoInfo>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionGeoInfo>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SessionGeoInfo)} does not support writing '{format}' format.");
            }
            if (Optional.IsDefined(CountryCode))
            {
                writer.WritePropertyName("country_code"u8);
                writer.WriteStringValue(CountryCode);
            }
            if (Optional.IsDefined(CountryName))
            {
                writer.WritePropertyName("country_name"u8);
                writer.WriteStringValue(CountryName);
            }
            if (Optional.IsDefined(Region))
            {
                writer.WritePropertyName("region"u8);
                writer.WriteStringValue(Region);
            }
            if (Optional.IsDefined(City))
            {
                writer.WritePropertyName("city"u8);
                writer.WriteStringValue(City);
            }
            if (Optional.IsDefined(PostalCode))
            {
                writer.WritePropertyName("postal_code"u8);
                writer.WriteStringValue(PostalCode);
            }
            if (Optional.IsDefined(Timezone))
            {
                writer.WritePropertyName("timezone"u8);
                writer.WriteStringValue(Timezone);
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

        SessionGeoInfo IJsonModel<SessionGeoInfo>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SessionGeoInfo JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionGeoInfo>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SessionGeoInfo)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSessionGeoInfo(document.RootElement, options);
        }

        internal static SessionGeoInfo DeserializeSessionGeoInfo(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string countryCode = default;
            string countryName = default;
            string region = default;
            string city = default;
            string postalCode = default;
            string timezone = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("country_code"u8))
                {
                    countryCode = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("country_name"u8))
                {
                    countryName = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("region"u8))
                {
                    region = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("city"u8))
                {
                    city = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("postal_code"u8))
                {
                    postalCode = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("timezone"u8))
                {
                    timezone = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SessionGeoInfo(
                countryCode,
                countryName,
                region,
                city,
                postalCode,
                timezone,
                additionalBinaryDataProperties);
        }
    }
}
