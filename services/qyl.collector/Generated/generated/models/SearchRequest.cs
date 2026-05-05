
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Search
{

    public partial class SearchRequest
    {
        public string Query { get; set; }

        [JsonPropertyName("entity_types")]
        public SearchEntityType[] EntityTypes { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [NumericConstraint<int>(MinValue = 1, MaxValue = 100)]
        public int? Limit { get; set; } = 20;

        public string Cursor { get; set; }


    }
}
