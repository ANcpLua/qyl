
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Traces;
using Trace = Qyl.OTel.Traces.Trace;

namespace Qyl.Api.Streaming
{

    public partial class TraceStreamEvent
    {
        [JsonPropertyName("type")]
        public string TypeName { get; } = "trace";

        public Trace Data { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
