
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Search
{
    public partial class SearchResponse : IJsonModel<SearchResponse>
    {
        internal SearchResponse()
        {
        }

        protected virtual SearchResponse PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SearchResponse>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSearchResponse(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SearchResponse)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SearchResponse>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SearchResponse)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SearchResponse>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SearchResponse IPersistableModel<SearchResponse>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SearchResponse>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static explicit operator SearchResponse(ClientResult result)
        {
            PipelineResponse response = result.GetRawResponse();
            using JsonDocument document = JsonDocument.Parse(response.Content, ModelSerializationExtensions.JsonDocumentOptions);
            return DeserializeSearchResponse(document.RootElement, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<SearchResponse>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SearchResponse>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SearchResponse)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("results"u8);
            writer.WriteStartArray();
            foreach (SearchResult item in Results)
            {
                writer.WriteObjectValue(item, options);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("total_count"u8);
            writer.WriteNumberValue(TotalCount);
            writer.WritePropertyName("duration_ms"u8);
            writer.WriteNumberValue(DurationMs);
            if (Optional.IsDefined(NextCursor))
            {
                writer.WritePropertyName("next_cursor"u8);
                writer.WriteStringValue(NextCursor);
            }
            if (Optional.IsCollectionDefined(Suggestions))
            {
                writer.WritePropertyName("suggestions"u8);
                writer.WriteStartArray();
                foreach (string item in Suggestions)
                {
                    if (item == null)
                    {
                        writer.WriteNullValue();
                        continue;
                    }
                    writer.WriteStringValue(item);
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

        SearchResponse IJsonModel<SearchResponse>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SearchResponse JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SearchResponse>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SearchResponse)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSearchResponse(document.RootElement, options);
        }

        internal static SearchResponse DeserializeSearchResponse(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            IList<SearchResult> results = default;
            long totalCount = default;
            int durationMs = default;
            string nextCursor = default;
            IList<string> suggestions = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("results"u8))
                {
                    List<SearchResult> array = new List<SearchResult>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(SearchResult.DeserializeSearchResult(item, options));
                    }
                    results = array;
                    continue;
                }
                if (prop.NameEquals("total_count"u8))
                {
                    totalCount = prop.Value.GetInt64();
                    continue;
                }
                if (prop.NameEquals("duration_ms"u8))
                {
                    durationMs = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("next_cursor"u8))
                {
                    nextCursor = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("suggestions"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<string> array = new List<string>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Null)
                        {
                            array.Add(null);
                        }
                        else
                        {
                            array.Add(item.GetString());
                        }
                    }
                    suggestions = array;
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SearchResponse(
                results,
                totalCount,
                durationMs,
                nextCursor,
                suggestions ?? new ChangeTrackingList<string>(),
                additionalBinaryDataProperties);
        }
    }
}
