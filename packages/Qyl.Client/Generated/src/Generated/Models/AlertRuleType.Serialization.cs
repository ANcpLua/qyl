
#nullable disable

using System;

namespace Qyl.Domains.Alerting
{
    internal static partial class AlertRuleTypeExtensions
    {
        public static string ToSerialString(this AlertRuleType value) => value switch
        {
            AlertRuleType.Threshold => "threshold",
            AlertRuleType.ErrorRate => "error_rate",
            AlertRuleType.NewIssue => "new_issue",
            AlertRuleType.Regression => "regression",
            AlertRuleType.BurnRate => "burn_rate",
            AlertRuleType.Anomaly => "anomaly",
            AlertRuleType.Custom => "custom",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AlertRuleType value.")
        };

        public static AlertRuleType ToAlertRuleType(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "threshold"))
            {
                return AlertRuleType.Threshold;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "error_rate"))
            {
                return AlertRuleType.ErrorRate;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "new_issue"))
            {
                return AlertRuleType.NewIssue;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "regression"))
            {
                return AlertRuleType.Regression;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "burn_rate"))
            {
                return AlertRuleType.BurnRate;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "anomaly"))
            {
                return AlertRuleType.Anomaly;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "custom"))
            {
                return AlertRuleType.Custom;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AlertRuleType value.");
        }
    }
}
