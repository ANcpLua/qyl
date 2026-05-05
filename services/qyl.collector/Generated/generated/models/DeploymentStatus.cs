
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Ops.Deployment
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeploymentStatus
    {

        [JsonStringEnumMemberName("pending")]
        Pending,

        [JsonStringEnumMemberName("in_progress")]
        InProgress,

        [JsonStringEnumMemberName("success")]
        Success,

        [JsonStringEnumMemberName("failed")]
        Failed,

        [JsonStringEnumMemberName("rolled_back")]
        RolledBack,

        [JsonStringEnumMemberName("cancelled")]
        Cancelled
    }
}
