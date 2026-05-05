
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Workflow
{

    public partial class WorkflowEventEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("run_id")]
        public string RunId { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; }

        [JsonPropertyName("event_type")]
        public string EventType { get; set; }

        [JsonPropertyName("event_name")]
        public string EventName { get; set; }

        [JsonPropertyName("payload_json")]
        public string PayloadJson { get; set; }

        [JsonPropertyName("sequence_number")]
        public long SequenceNumber { get; set; }

        public string Source { get; set; }

        [JsonPropertyName("correlation_id")]
        public string CorrelationId { get; set; }

        public DateTimeOffset Timestamp { get; set; }


    }
}
