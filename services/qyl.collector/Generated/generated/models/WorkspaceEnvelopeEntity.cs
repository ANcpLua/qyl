
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Workspace
{

    public partial class WorkspaceEnvelopeEntity
    {
        public string Id { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("environment_id")]
        public string EnvironmentId { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; }

        public string Name { get; set; }

        [JsonPropertyName("root_path")]
        public string RootPath { get; set; }

        [JsonPropertyName("heartbeat_at")]
        public DateTimeOffset? HeartbeatAt { get; set; }

        [JsonPropertyName("heartbeat_interval_seconds")]
        public int HeartbeatIntervalSeconds { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public WorkspaceStatus Status { get; set; }

        [JsonPropertyName("config_json")]
        public string ConfigJson { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }


    }
}
