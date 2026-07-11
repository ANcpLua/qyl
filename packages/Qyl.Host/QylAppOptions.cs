using Microsoft.Extensions.Configuration;

namespace Qyl.Host;

public sealed class QylAppOptions
{
    public const string SectionName = "Qyl:Host";

    public int RunnerPort { get; init; } = QylConstants.Ports.RunnerApi;

    public string RunnerHost { get; init; } = QylConstants.Network.Loopback;

    public int StartupTimeoutSeconds { get; init; } = QylConstants.Orchestrator.StartupTimeoutSeconds;

    // Manual, reflection-free bind: ConfigurationBinder and DataAnnotations validation both walk the type
    // via reflection, which the trimmer cannot see through. Reading the known keys explicitly keeps
    // env-var/appsettings overrides working while the options path stays trim/AOT-clean, and calling this
    // from Build() keeps the old ValidateOnStart fail-fast semantics.
    public static QylAppOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var options = new QylAppOptions
        {
            RunnerPort = ReadInt(section, nameof(RunnerPort), QylConstants.Ports.RunnerApi),
            RunnerHost = section[nameof(RunnerHost)] ?? QylConstants.Network.Loopback,
            StartupTimeoutSeconds =
                ReadInt(section, nameof(StartupTimeoutSeconds), QylConstants.Orchestrator.StartupTimeoutSeconds)
        };

        if (options.RunnerPort is < 0 or > 65535)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(RunnerPort)} must be a valid TCP port (0 = auto-allocate)");
        }

        if (string.IsNullOrWhiteSpace(options.RunnerHost))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(RunnerHost)} is required");
        }

        if (options.StartupTimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(StartupTimeoutSeconds)} must be 1..600");
        }

        return options;
    }

    private static int ReadInt(IConfiguration section, string key, int fallback)
    {
        var raw = section[key];
        if (raw is null) return fallback;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"{SectionName}:{key} must be an integer, got '{raw}'");
    }
}
