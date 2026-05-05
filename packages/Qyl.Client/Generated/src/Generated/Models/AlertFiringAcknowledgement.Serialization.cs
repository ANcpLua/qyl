
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class AlertFiringAcknowledgement : IJsonModel<AlertFiringAcknowledgement>
    {
        internal AlertFiringAcknowledgement()
        {
        }

        protected virtual AlertFiringAcknowledgement PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<AlertFiringAcknowledgement>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeAlertFiringAcknowledgement(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(AlertFiringAcknowledgement)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<AlertFiringAcknowledgement>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(AlertFiringAcknowledgement)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<AlertFiringAcknowledgement>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        AlertFiringAcknowledgement IPersistableModel<AlertFiringAcknowledgement>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<AlertFiringAcknowledgement>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(AlertFiringAcknowledgement alertFiringAcknowledgement)
        {
            if (alertFiringAcknowledgement == null)
            {
                return null;
            }
            return BinaryContent.Create(alertFiringAcknowledgement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<AlertFiringAcknowledgement>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<AlertFiringAcknowledgement>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(AlertFiringAcknowledgement)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("acknowledged_by"u8);
            writer.WriteStringValue(AcknowledgedBy);
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

        AlertFiringAcknowledgement IJsonModel<AlertFiringAcknowledgement>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual AlertFiringAcknowledgement JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<AlertFiringAcknowledgement>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(AlertFiringAcknowledgement)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeAlertFiringAcknowledgement(document.RootElement, options);
        }

        internal static AlertFiringAcknowledgement DeserializeAlertFiringAcknowledgement(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string acknowledgedBy = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("acknowledged_by"u8))
                {
                    acknowledgedBy = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new AlertFiringAcknowledgement(acknowledgedBy, additionalBinaryDataProperties);
        }
    }
}
