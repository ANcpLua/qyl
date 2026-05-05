
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionStats : IJsonModel<SessionStats>
    {
        internal SessionStats()
        {
        }

        protected virtual SessionStats PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSessionStats(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SessionStats)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionStats>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SessionStats)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SessionStats>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SessionStats IPersistableModel<SessionStats>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SessionStats>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator SessionStats(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeSessionStats(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<SessionStats>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SessionStats)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("active_sessions"u8);
            writer.WriteNumberValue(ActiveSessions);
            writer.WritePropertyName("total_sessions"u8);
            writer.WriteNumberValue(TotalSessions);
            writer.WritePropertyName("unique_users"u8);
            writer.WriteNumberValue(UniqueUsers);
            writer.WritePropertyName("avg_duration_ms"u8);
            writer.WriteNumberValue(AvgDurationMs);
            writer.WritePropertyName("sessions_with_errors"u8);
            writer.WriteNumberValue(SessionsWithErrors);
            writer.WritePropertyName("sessions_with_genai"u8);
            writer.WriteNumberValue(SessionsWithGenAi);
            writer.WritePropertyName("bounce_rate"u8);
            writer.WriteNumberValue(BounceRate);
            if (Optional.IsCollectionDefined(ByDeviceType))
            {
                writer.WritePropertyName("by_device_type"u8);
                writer.WriteStartArray();
                foreach (SessionDeviceStats item in ByDeviceType)
                {
                    writer.WriteObjectValue(item, options);
                }
                writer.WriteEndArray();
            }
            if (Optional.IsCollectionDefined(ByCountry))
            {
                writer.WritePropertyName("by_country"u8);
                writer.WriteStartArray();
                foreach (SessionCountryStats item in ByCountry)
                {
                    writer.WriteObjectValue(item, options);
                }
                writer.WriteEndArray();
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

        SessionStats IJsonModel<SessionStats>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SessionStats JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SessionStats>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SessionStats)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSessionStats(document.RootElement, options);
        }

        internal static SessionStats DeserializeSessionStats(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            long activeSessions = default;
            long totalSessions = default;
            long uniqueUsers = default;
            double avgDurationMs = default;
            long sessionsWithErrors = default;
            long sessionsWithGenAi = default;
            double bounceRate = default;
            IList<SessionDeviceStats> byDeviceType = default;
            IList<SessionCountryStats> byCountry = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("active_sessions"u8))
                {
                    activeSessions = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("total_sessions"u8))
                {
                    totalSessions = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("unique_users"u8))
                {
                    uniqueUsers = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("avg_duration_ms"u8))
                {
                    avgDurationMs = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("sessions_with_errors"u8))
                {
                    sessionsWithErrors = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("sessions_with_genai"u8))
                {
                    sessionsWithGenAi = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("bounce_rate"u8))
                {
                    bounceRate = prop.Value.GetDouble();
                    continue;
                }
                if (prop.NameEquals("by_device_type"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<SessionDeviceStats> array = new List<SessionDeviceStats>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(SessionDeviceStats.DeserializeSessionDeviceStats(item, options));
                    }
                    byDeviceType = array;
                    continue;
                }
                if (prop.NameEquals("by_country"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<SessionCountryStats> array = new List<SessionCountryStats>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(SessionCountryStats.DeserializeSessionCountryStats(item, options));
                    }
                    byCountry = array;
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SessionStats(
                activeSessions,
                totalSessions,
                uniqueUsers,
                avgDurationMs,
                sessionsWithErrors,
                sessionsWithGenAi,
                bounceRate,
                byDeviceType ?? new ChangeTrackingList<SessionDeviceStats>(),
                byCountry ?? new ChangeTrackingList<SessionCountryStats>(),
                additionalBinaryDataProperties);
        }
    }
}
