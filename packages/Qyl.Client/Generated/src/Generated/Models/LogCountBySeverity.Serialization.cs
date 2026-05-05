
#nullable disable

using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;
using Qyl.OTel.Enums;

namespace Qyl.OTel.Logs
{
    public partial class LogCountBySeverity : IJsonModel<LogCountBySeverity>
    {
        internal LogCountBySeverity()
        {
        }

        protected virtual LogCountBySeverity PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogCountBySeverity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeLogCountBySeverity(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(LogCountBySeverity)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogCountBySeverity>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(LogCountBySeverity)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<LogCountBySeverity>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        LogCountBySeverity IPersistableModel<LogCountBySeverity>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<LogCountBySeverity>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        void IJsonModel<LogCountBySeverity>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogCountBySeverity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogCountBySeverity)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("severity"u8);
            writer.WriteStringValue(Severity.ToSerialString());
            writer.WritePropertyName("count"u8);
            writer.WriteNumberValue(Count);
            writer.WritePropertyName("percentage"u8);
            writer.WriteNumberValue(Percentage);
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

        LogCountBySeverity IJsonModel<LogCountBySeverity>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual LogCountBySeverity JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<LogCountBySeverity>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(LogCountBySeverity)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeLogCountBySeverity(document.RootElement, options);
        }

        internal static LogCountBySeverity DeserializeLogCountBySeverity(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            SeverityText severity = default;
            long count = default;
            double percentage = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("severity"u8))
                {
                    severity = prop.Value.GetString().ToSeverityText();
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
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new LogCountBySeverity(severity, count, percentage, additionalBinaryDataProperties);
        }
    }
}
