
#nullable disable

namespace Qyl.Domains.Workflow
{
    public enum WorkflowRunStatus
    {
        Pending,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled,
        TimedOut
    }
}
