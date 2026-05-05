
#nullable disable

namespace Qyl.Domains.Alerting
{
    public enum FixRunStatus
    {
        Pending,
        Running,
        AwaitingApproval,
        Applied,
        Rejected,
        Failed
    }
}
