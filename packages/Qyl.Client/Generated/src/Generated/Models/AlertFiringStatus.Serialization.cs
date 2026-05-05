
#nullable disable

using System;

namespace Qyl.Domains.Alerting
{
    internal static partial class AlertFiringStatusExtensions
    {
        public static string ToSerialString(this AlertFiringStatus value) => value switch
        {
            AlertFiringStatus.Firing => "firing",
            AlertFiringStatus.Acknowledged => "acknowledged",
            AlertFiringStatus.Resolved => "resolved",
            AlertFiringStatus.Suppressed => "suppressed",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AlertFiringStatus value.")
        };

        public static AlertFiringStatus ToAlertFiringStatus(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "firing"))
            {
                return AlertFiringStatus.Firing;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "acknowledged"))
            {
                return AlertFiringStatus.Acknowledged;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "resolved"))
            {
                return AlertFiringStatus.Resolved;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "suppressed"))
            {
                return AlertFiringStatus.Suppressed;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AlertFiringStatus value.");
        }
    }
}
