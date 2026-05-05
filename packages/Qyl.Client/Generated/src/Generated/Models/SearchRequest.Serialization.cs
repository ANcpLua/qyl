
#nullable disable

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using Qyl.Client;

namespace Qyl.Domains.Search
{
    public partial class SearchRequest : IJsonModel<SearchRequest>
    {
        internal SearchRequest()
        {
        }

        protected virtual SearchRequest PersistableModelCreateCore(BinaryData data, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SearchRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    using (JsonDocument document = JsonDocument.Parse(data, ModelSerializationExtensions.JsonDocumentOptions))
                    {
                        return DeserializeSearchRequest(document.RootElement, options);
                    }
                default:
                    throw new FormatException($"The model {nameof(SearchRequest)} does not support reading '{options.Format}' format.");
            }
        }

        protected virtual BinaryData PersistableModelWriteCore(ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SearchRequest>)this).GetFormatFromOptions(options) : options.Format;
            switch (format)
            {
                case "J":
                    return ModelReaderWriter.Write(this, options, QylClientContext.Default);
                default:
                    throw new FormatException($"The model {nameof(SearchRequest)} does not support writing '{options.Format}' format.");
            }
        }

        BinaryData IPersistableModel<SearchRequest>.Write(ModelReaderWriterOptions options) => PersistableModelWriteCore(options);

        SearchRequest IPersistableModel<SearchRequest>.Create(BinaryData data, ModelReaderWriterOptions options) => PersistableModelCreateCore(data, options);

        string IPersistableModel<SearchRequest>.GetFormatFromOptions(ModelReaderWriterOptions options) => "J";

        public static implicit operator BinaryContent(SearchRequest searchRequest)
        {
            if (searchRequest == null)
            {
                return null;
            }
            return BinaryContent.Create(searchRequest, ModelSerializationExtensions.WireOptions);
        }

        void IJsonModel<SearchRequest>.Write(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            writer.WriteStartObject();
            JsonModelWriteCore(writer, options);
            writer.WriteEndObject();
        }

        protected virtual void JsonModelWriteCore(Utf8JsonWriter writer, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SearchRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SearchRequest)} does not support writing '{format}' format.");
            }
            writer.WritePropertyName("query"u8);
            writer.WriteStringValue(Query);
            if (Optional.IsCollectionDefined(EntityTypes))
            {
                writer.WritePropertyName("entity_types"u8);
                writer.WriteStartArray();
                foreach (SearchEntityType item in EntityTypes)
                {
                    writer.WriteStringValue(item.ToSerialString());
                }
                writer.WriteEndArray();
            }
            if (Optional.IsDefined(ProjectId))
            {
                writer.WritePropertyName("project_id"u8);
                writer.WriteStringValue(ProjectId);
            }
            if (Optional.IsDefined(Limit))
            {
                writer.WritePropertyName("limit"u8);
                writer.WriteNumberValue(Limit.Value);
            }
            if (Optional.IsDefined(Cursor))
            {
                writer.WritePropertyName("cursor"u8);
                writer.WriteStringValue(Cursor);
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

        SearchRequest IJsonModel<SearchRequest>.Create(ref Utf8JsonReader reader, ModelReaderWriterOptions options) => JsonModelCreateCore(ref reader, options);

        protected virtual SearchRequest JsonModelCreateCore(ref Utf8JsonReader reader, ModelReaderWriterOptions options)
        {
            string format = options.Format == "W" ? ((IPersistableModel<SearchRequest>)this).GetFormatFromOptions(options) : options.Format;
            if (format != "J")
            {
                throw new FormatException($"The model {nameof(SearchRequest)} does not support reading '{format}' format.");
            }
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return DeserializeSearchRequest(document.RootElement, options);
        }

        internal static SearchRequest DeserializeSearchRequest(JsonElement element, ModelReaderWriterOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            string query = default;
            IList<SearchEntityType> entityTypes = default;
            string projectId = default;
            int? limit = default;
            string cursor = default;
            IDictionary<string, BinaryData> additionalBinaryDataProperties = new ChangeTrackingDictionary<string, BinaryData>();
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("query"u8))
                {
                    query = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("entity_types"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<SearchEntityType> array = new List<SearchEntityType>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        array.Add(item.GetString().ToSearchEntityType());
                    }
                    entityTypes = array;
                    continue;
                }
                if (prop.NameEquals("project_id"u8))
                {
                    projectId = prop.Value.GetString();
                    continue;
                }
                if (prop.NameEquals("limit"u8))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    limit = prop.Value.GetInt32();
                    continue;
                }
                if (prop.NameEquals("cursor"u8))
                {
                    cursor = prop.Value.GetString();
                    continue;
                }
                if (options.Format != "W")
                {
                    additionalBinaryDataProperties.Add(prop.Name, BinaryData.FromString(prop.Value.GetRawText()));
                }
            }
            return new SearchRequest(
                query,
                entityTypes ?? new ChangeTrackingList<SearchEntityType>(),
                projectId,
                limit,
                cursor,
                additionalBinaryDataProperties);
        }
    }
}
