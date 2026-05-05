
#nullable disable

namespace Qyl.Domains.Ops.Deployment
{
    public enum DeploymentStatus
    {
        Pending,
        InProgress,
        Success,
        Failed,
        RolledBack,
        Cancelled
    }
}
