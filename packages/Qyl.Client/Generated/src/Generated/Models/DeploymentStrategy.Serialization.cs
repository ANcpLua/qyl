
#nullable disable

using System;

namespace Qyl.Domains.Ops.Deployment
{
    internal static partial class DeploymentStrategyExtensions
    {
        public static string ToSerialString(this DeploymentStrategy value) => value switch
        {
            DeploymentStrategy.Rolling => "rolling",
            DeploymentStrategy.BlueGreen => "blue_green",
            DeploymentStrategy.Canary => "canary",
            DeploymentStrategy.Recreate => "recreate",
            DeploymentStrategy.AbTest => "ab_test",
            DeploymentStrategy.Shadow => "shadow",
            DeploymentStrategy.FeatureFlag => "feature_flag",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown DeploymentStrategy value.")
        };

        public static DeploymentStrategy ToDeploymentStrategy(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "rolling"))
            {
                return DeploymentStrategy.Rolling;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "blue_green"))
            {
                return DeploymentStrategy.BlueGreen;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "canary"))
            {
                return DeploymentStrategy.Canary;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "recreate"))
            {
                return DeploymentStrategy.Recreate;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "ab_test"))
            {
                return DeploymentStrategy.AbTest;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "shadow"))
            {
                return DeploymentStrategy.Shadow;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "feature_flag"))
            {
                return DeploymentStrategy.FeatureFlag;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown DeploymentStrategy value.");
        }
    }
}
