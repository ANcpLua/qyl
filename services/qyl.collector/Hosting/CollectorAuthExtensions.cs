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

        if (otlpApiKeyOptions is { IsApiKeyMode: true, Keys.Count: 0 })
        {
            throw new InvalidOperationException(
                "OTLP auth mode is 'ApiKey' but no project keys are configured. " +
                "Set QYL_OTLP_PROJECT_KEYS to a JSON object mapping project IDs to arrays of {\"key\": string, \"roles\": [...]} objects.");
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
                foreach (var entry in project.Value.EnumerateArray())
                {
                    var (value, roles) = ParseCredential(entry);
                    if (!credentials.Add(value))
                        throw new InvalidOperationException("QYL_OTLP_PROJECT_KEYS has an invalid or duplicate credential.");
                    keys.Add(new ProjectApiKey(project.Name, value, roles));
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

    // Every credential declares its permissions; no implicit roles are granted.
    private static (string Key, ApiKeyRoles Roles) ParseCredential(JsonElement entry)
    {
        if (entry.ValueKind is not JsonValueKind.Object ||
            !entry.TryGetProperty("key", out var keyElement) || keyElement.ValueKind is not JsonValueKind.String ||
            string.IsNullOrWhiteSpace(keyElement.GetString()) ||
            !entry.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind is not JsonValueKind.Array ||
            entry.EnumerateObject().Count() is not 2 ||
            entry.EnumerateObject().Any(static property => property.Name is not ("key" or "roles")))
        {
            throw new InvalidOperationException(
                "QYL_OTLP_PROJECT_KEYS credentials must be {\"key\": string, \"roles\": [...]} objects.");
        }

        var roles = ApiKeyRoles.None;
        foreach (var roleElement in rolesElement.EnumerateArray())
        {
            if (roleElement.ValueKind is not JsonValueKind.String ||
                !ApiKeyRoleNames.TryParse(roleElement.GetString(), out var role) ||
                (roles & role) is not ApiKeyRoles.None)
            {
                throw new InvalidOperationException(
                    "QYL_OTLP_PROJECT_KEYS roles must be distinct values of ingest, read, write and delete.");
            }

            roles |= role;
        }

        return roles is ApiKeyRoles.None
            ? throw new InvalidOperationException("QYL_OTLP_PROJECT_KEYS has a credential without roles.")
            : (keyElement.GetString()!, roles);
    }
}
