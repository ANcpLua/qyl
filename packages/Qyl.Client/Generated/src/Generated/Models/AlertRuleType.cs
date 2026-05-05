
#nullable disable

namespace Qyl.Domains.Alerting
{
    public enum AlertRuleType
    {
        Threshold,
        ErrorRate,
        NewIssue,
        Regression,
        BurnRate,
        Anomaly,
        Custom
    }
}
