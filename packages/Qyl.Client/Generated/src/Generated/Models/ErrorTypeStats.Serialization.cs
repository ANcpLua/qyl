
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorTypeStats : IJsonModel<ErrorTypeStats>
    {
        internal ErrorTypeStats()
        {
        }

        protected virtual ErrorTypeStats PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorTypeStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeErrorTypeStats(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(ErrorTypeStats)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorTypeStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(ErrorTypeStats)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<ErrorTypeStats>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        ErrorTypeStats IPersistableModel<ErrorTypeStats>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<ErrorTypeStats>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<ErrorTypeStats>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorTypeStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorTypeStats)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("error_type"u8);
            writer.WriteStringValue(ErrorType);
            writer.WritePropertyName("count"u8);
            writer.WriteNumberValue(Count);
            writer.WritePropertyName("percentage"u8);
            writer.WriteNumberValue(Percentage);
            if (Optional.IsDefined(AffectedUsers))
            {
                writer.WritePropertyName("affected_users"u8);
                writer.WriteNumberValue(AffectedUsers.Value);
            }
            writer.WritePropertyName("status"u8);
            writer.WriteStringValue(Status.ToSerialString());
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

        ErrorTypeStats IJsonModel<ErrorTypeStats>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual ErrorTypeStats JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<ErrorTypeStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(ErrorTypeStats)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeErrorTypeStats(document.RootElement, options);
        }

        internal static ErrorTypeStats DeserializeErrorTypeStats(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string errorType = default;
            long count = default;
            double percentage = default;
            long? affectedUsers = default;
            ErrorStatus status = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("error_type"u8))
                {
                    errorType = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("count"u8))
                {
                    count = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("percentage"u8))
                {
                    percentage = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("affected_users"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    affectedUsers = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("status"u8))
                {
                    status = prop.Value.GetString().ToErrorStatus();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new ErrorTypeStats(
                errorType,
                count,
                percentage,
                affectedUsers,
                status,
                additionalBinaryDataProperties);
        }
    }
}
