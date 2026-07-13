
namespace Qyl.Host;

public interface IQylResourceBuilder
{
    string Name { get; }
}

internal sealed class QylResourceBuilder(
    QylAppBuilder app,
    QylResource resource,
    Action<QylResource, QylResource> replace) : IQylResourceBuilder
{
    internal QylAppBuilder App { get; } = app;

    internal QylResource Resource { get; private set; } = resource;

    public string Name => Resource.Name;

    internal IQylResourceBuilder Update(Func<QylResource, QylResource> mutate)
    {
        var updated = mutate(Resource);
        replace(Resource, updated);
        Resource = updated;
        return this;
    }
}
