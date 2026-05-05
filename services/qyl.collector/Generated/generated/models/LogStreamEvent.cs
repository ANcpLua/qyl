
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Logs;

namespace Qyl.Api.Streaming
{

    public partial class LogStreamEvent
    {
        [JsonPropertyName("type")]
        public string TypeName { get; } = "log";

        public LogRecord Data { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
