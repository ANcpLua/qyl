
namespace Qyl.Run;

public interface IQylResourceBuilder
{
    QylAppBuilder App { get; }
    QylResource Resource { get; }

    IQylResourceBuilder Update(Func<QylResource, QylResource> mutate);
}

internal sealed class QylResourceBuilder(
    QylAppBuilder app,
    QylResource resource,
    Action<QylResource, QylResource> replace) : IQylResourceBuilder
{
    public QylAppBuilder App { get; } = app;

    public QylResource Resource { get; private set; } = resource;

    public IQylResourceBuilder Update(Func<QylResource, QylResource> mutate)
    {
        var updated = mutate(Resource);
        replace(Resource, updated);
        Resource = updated;
        return this;
    }
}
