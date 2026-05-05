
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Workflow
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WorkflowRunStatus
    {

        [JsonStringEnumMemberName("pending")]
        Pending,

        [JsonStringEnumMemberName("running")]
        Running,

        [JsonStringEnumMemberName("paused")]
        Paused,

        [JsonStringEnumMemberName("completed")]
        Completed,

        [JsonStringEnumMemberName("failed")]
        Failed,

        [JsonStringEnumMemberName("cancelled")]
        Cancelled,

        [JsonStringEnumMemberName("timed_out")]
        TimedOut
    }
}
