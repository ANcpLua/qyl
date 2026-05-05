
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Alerting
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AlertRuleType
    {

        [JsonStringEnumMemberName("threshold")]
        Threshold,

        [JsonStringEnumMemberName("error_rate")]
        ErrorRate,

        [JsonStringEnumMemberName("new_issue")]
        NewIssue,

        [JsonStringEnumMemberName("regression")]
        Regression,

        [JsonStringEnumMemberName("burn_rate")]
        BurnRate,

        [JsonStringEnumMemberName("anomaly")]
        Anomaly,

        [JsonStringEnumMemberName("custom")]
        Custom
    }
}
