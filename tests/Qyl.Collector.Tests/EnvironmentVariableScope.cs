namespace Qyl.Collector.Tests;

/// <summary>
/// Sets process environment variables for the lifetime of the scope and restores their previous
/// values on disposal. Tests that use it run in a <see cref="DoNotParallelizeAttribute"/> class.
/// </summary>
internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly (string Name, string? Previous)[] _previous;

    public EnvironmentVariableScope(IReadOnlyDictionary<string, string> variables)
    {
        _previous = [.. variables.Keys.Select(static name => (name, Environment.GetEnvironmentVariable(name)))];
        foreach (var (name, value) in variables)
            Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        foreach (var (name, previous) in _previous)
            Environment.SetEnvironmentVariable(name, previous);
    }
}
