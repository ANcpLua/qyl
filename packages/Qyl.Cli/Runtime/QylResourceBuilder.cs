
using System;

namespace Qyl.Cli.Runtime;

internal sealed class QylResourceBuilder(
    QylResource resource,
    Action<QylResource, QylResource> replace)
{
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
