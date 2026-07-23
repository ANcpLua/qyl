
namespace Qyl.Cli.Runtime;

internal sealed class QylResourceBuilder(
    QylAppBuilder app,
    QylResource resource,
    Action<QylResource, QylResource> replace)
{
    internal QylAppBuilder App { get; } = app;

    internal QylResource Resource { get; private set; } = resource;

    internal string Name => Resource.Name;

    internal QylResourceBuilder Update(Func<QylResource, QylResource> mutate)
    {
        var updated = mutate(Resource);
        replace(Resource, updated);
        Resource = updated;
        return this;
    }
}
