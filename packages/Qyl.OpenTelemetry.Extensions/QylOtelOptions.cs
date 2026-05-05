
namespace Qyl.OpenTelemetry.Extensions;

public sealed class QylOtelOptions
{
    public Uri? Endpoint { get; set; }

    public string? ServiceName { get; set; }

    public string? ApiKey { get; set; }

    public double SampleRate { get; set; } = 1.0;
}
