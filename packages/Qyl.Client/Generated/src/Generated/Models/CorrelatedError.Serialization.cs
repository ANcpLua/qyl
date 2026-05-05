
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Error
{
    public partial class CorrelatedError : IJsonModel<CorrelatedError>
    {
        internal CorrelatedError()
        {
        }

        protected virtual CorrelatedError PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CorrelatedError>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeCorrelatedError(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(CorrelatedError)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CorrelatedError>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(CorrelatedError)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<CorrelatedError>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        CorrelatedError IPersistableModel<CorrelatedError>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<CorrelatedError>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<CorrelatedError>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CorrelatedError>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(CorrelatedError)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("error_id"u8);
            writer.WriteStringValue(ErrorId);
            writer.WritePropertyName("error_type"u8);
            writer.WriteStringValue(ErrorType);
            writer.WritePropertyName("correlation_strength"u8);
            writer.WriteNumberValue(CorrelationStrength);
            writer.WritePropertyName("temporal_relationship"u8);
            writer.WriteStringValue(TemporalRelationship.ToSerialString());
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

        CorrelatedError IJsonModel<CorrelatedError>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual CorrelatedError JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<CorrelatedError>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(CorrelatedError)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeCorrelatedError(document.RootElement, options);
        }

        internal static CorrelatedError DeserializeCorrelatedError(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string errorId = default;
            string errorType = default;
            double correlationStrength = default;
            TemporalRelationship temporalRelationship = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("error_id"u8))
                {
                    errorId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("error_type"u8))
                {
                    errorType = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("correlation_strength"u8))
                {
                    correlationStrength = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("temporal_relationship"u8))
                {
                    temporalRelationship = prop.Value.GetString().ToTemporalRelationship();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new CorrelatedError(errorId, errorType, correlationStrength, temporalRelationship, additionalBinaryDataProperties);
        }
    }
}
