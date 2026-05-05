
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Issues
{

    public partial class ErrorBreadcrumbEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("breadcrumb_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public BreadcrumbType BreadcrumbType { get; set; }

        public string Category { get; set; }

        public string Message { get; set; }

        public string Level { get; set; }

        [JsonPropertyName("data_json")]
        public string DataJson { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
