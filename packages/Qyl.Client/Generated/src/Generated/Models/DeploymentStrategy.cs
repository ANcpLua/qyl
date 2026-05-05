
#nullable disable

namespace Qyl.Domains.Ops.Deployment
{
    public enum DeploymentStrategy
    {
        Rolling,
        BlueGreen,
        Canary,
        Recreate,
        AbTest,
        Shadow,
        FeatureFlag
    }
}
