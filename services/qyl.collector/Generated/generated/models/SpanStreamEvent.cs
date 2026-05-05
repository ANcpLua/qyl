
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Traces;

namespace Qyl.Api.Streaming
{

    public partial class SpanStreamEvent
    {
        [JsonPropertyName("type")]
        public string TypeName { get; } = "span";

        public Span Data { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
