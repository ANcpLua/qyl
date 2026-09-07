namespace Qyl.Api.Tests;

/// <summary>
/// One collection, so these never run at the same time. They attach ActivityListeners to the
/// "Microsoft.AspNetCore" source by name, and that source is process-wide: in parallel, one class's AllData
/// listener decides the sampling of another class's activity, and the guard test for a non-recording span
/// passes or fails by scheduling.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ActivityListeners
{
    public const string Name = "activity-listeners";
}
