using Qyl.Collector.ApiKeys;

namespace Qyl.Collector.Hosting;

internal static class CollectorAuthExtensions
{
    public static IServiceCollection AddQylCollectorAuth(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment environment)
    {
        var otlpCorsOptions = new OtlpCorsOptions
        {
            AllowedOrigins = config["QYL_OTLP_CORS_ALLOWED_ORIGINS"],
            AllowedHeaders = config["QYL_OTLP_CORS_ALLOWED_HEADERS"]
        };
        services.AddSingleton(otlpCorsOptions);

        var configuredAuthMode = config["QYL_OTLP_AUTH_MODE"];
        var otlpApiKeyOptions = new OtlpApiKeyOptions
        {
            AuthMode = configuredAuthMode ?? (environment.IsDevelopment() ? "Unsecured" : "ApiKey"),
            Keys = ParseProjectKeys(config["QYL_OTLP_PROJECT_KEYS"])
        };

        if (otlpApiKeyOptions.IsApiKeyMode && otlpApiKeyOptions.Keys.Count is 0)
        {
            throw new InvalidOperationException(
                "OTLP auth mode is 'ApiKey' but no project keys are configured. " +
                "Set QYL_OTLP_PROJECT_KEYS to a JSON object mapping project IDs to arrays of API keys.");
        }

        services.AddSingleton(otlpApiKeyOptions);

        services.AddSingleton(TimeProvider.System);

        return services;
    }

    private static IReadOnlyList<ProjectApiKey> ParseProjectKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("QYL_OTLP_PROJECT_KEYS must be a JSON object.");

            var keys = new List<ProjectApiKey>();
            var projects = new HashSet<string>(StringComparer.Ordinal);
            var credentials = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in document.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(project.Name) || project.Name != project.Name.Trim() ||
                    !projects.Add(project.Name) || project.Value.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("QYL_OTLP_PROJECT_KEYS has an invalid or duplicate project.");

                var count = 0;
                foreach (var key in project.Value.EnumerateArray())
                {
                    var value = key.ValueKind == JsonValueKind.String ? key.GetString() : null;
                    if (string.IsNullOrWhiteSpace(value) || !credentials.Add(value))
                        throw new InvalidOperationException("QYL_OTLP_PROJECT_KEYS has an invalid or duplicate credential.");
                    keys.Add(new ProjectApiKey(project.Name, value));
                    count++;
                }

                if (count is 0)
                    throw new InvalidOperationException("QYL_OTLP_PROJECT_KEYS has a project without credentials.");
            }

            return keys;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("QYL_OTLP_PROJECT_KEYS must be valid JSON.", ex);
        }
    }
}
