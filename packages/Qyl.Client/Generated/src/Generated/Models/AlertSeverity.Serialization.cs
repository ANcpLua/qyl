
#nullable disable

using System;

namespace Qyl.Domains.Alerting
{
    internal static partial class AlertSeverityExtensions
    {
        public static string ToSerialString(this AlertSeverity value) => value switch
        {
            AlertSeverity.Critical => "critical",
            AlertSeverity.Warning => "warning",
            AlertSeverity.Info => "info",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AlertSeverity value.")
        };

        public static AlertSeverity ToAlertSeverity(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "critical"))
            {
                return AlertSeverity.Critical;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "warning"))
            {
                return AlertSeverity.Warning;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "info"))
            {
                return AlertSeverity.Info;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AlertSeverity value.");
        }
    }
}
