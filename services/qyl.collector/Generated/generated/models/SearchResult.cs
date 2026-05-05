
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Search
{

    public partial class SearchResult
    {
        [JsonPropertyName("document_id")]
        public string DocumentId { get; set; }

        [JsonPropertyName("entity_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SearchEntityType EntityType { get; set; }

        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; }

        public string Title { get; set; }

        public string Snippet { get; set; }

        public double Score { get; set; }

        public string Url { get; set; }


    }
}
