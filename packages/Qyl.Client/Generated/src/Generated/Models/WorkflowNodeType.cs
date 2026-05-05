
#nullable disable

namespace Qyl.Domains.Workflow
{
    public enum WorkflowNodeType
    {
        Agent,
        Tool,
        Condition,
        Fork,
        Join,
        Approval,
        SubWorkflow,
        Transform,
        Wait
    }
}
