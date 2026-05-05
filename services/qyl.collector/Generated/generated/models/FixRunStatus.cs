
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Alerting
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FixRunStatus
    {

        [JsonStringEnumMemberName("pending")]
        Pending,

        [JsonStringEnumMemberName("running")]
        Running,

        [JsonStringEnumMemberName("awaiting_approval")]
        AwaitingApproval,

        [JsonStringEnumMemberName("applied")]
        Applied,

        [JsonStringEnumMemberName("rejected")]
        Rejected,

        [JsonStringEnumMemberName("failed")]
        Failed
    }
}
