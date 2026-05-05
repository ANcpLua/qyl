
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Workflow
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WorkflowNodeType
    {

        [JsonStringEnumMemberName("agent")]
        Agent,

        [JsonStringEnumMemberName("tool")]
        Tool,

        [JsonStringEnumMemberName("condition")]
        Condition,

        [JsonStringEnumMemberName("fork")]
        Fork,

        [JsonStringEnumMemberName("join")]
        JoinName,

        [JsonStringEnumMemberName("approval")]
        Approval,

        [JsonStringEnumMemberName("sub_workflow")]
        SubWorkflow,

        [JsonStringEnumMemberName("transform")]
        Transform,

        [JsonStringEnumMemberName("wait")]
        Wait
    }
}
