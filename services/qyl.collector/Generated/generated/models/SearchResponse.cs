
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Search
{

    public partial class SearchResponse
    {
        public SearchResult[] Results { get; set; }

        [JsonPropertyName("total_count")]
        public long TotalCount { get; set; }

        [JsonPropertyName("duration_ms")]
        public int DurationMs { get; set; }

        [JsonPropertyName("next_cursor")]
        public string NextCursor { get; set; }

        public string[] Suggestions { get; set; }


    }
}
