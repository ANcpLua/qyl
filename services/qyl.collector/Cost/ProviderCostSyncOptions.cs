namespace Qyl.Collector.Cost;

internal sealed record ProviderCostSyncOptions
{
    public required string ProjectId { get; init; }

    public required TimeSpan SyncInterval { get; init; }

    public required int LookbackDays { get; init; }

    public string? OpenAiAdminKey { get; init; }

    public string? OpenAiProjectId { get; init; }

    public string? AnthropicAdminKey { get; init; }

    public ProviderCostScope AnthropicWorkspaceScope { get; init; } = ProviderCostScope.Organization;

    public ProviderCostScope ScopeFor(string provider) => provider switch
    {
        "openai" => OpenAiProjectId is null
            ? ProviderCostScope.Organization
            : ProviderCostScope.ForIdentifier(OpenAiProjectId),
        "anthropic" => AnthropicWorkspaceScope,
        _ => ProviderCostScope.Organization
    };

    public static ProviderCostSyncOptions FromConfiguration(IConfiguration configuration)
    {
        var projectId = ProjectScope.Normalize(configuration["QYL_COST_PROJECT_ID"]);
        if (projectId.Length > 128)
            throw new InvalidOperationException("QYL_COST_PROJECT_ID must be at most 128 characters.");

        var intervalMinutes = int.Parse(configuration["QYL_COST_SYNC_INTERVAL_MINUTES"] ?? "15", CultureInfo.InvariantCulture);
        if (intervalMinutes is < 1 or > 1440)
            throw new InvalidOperationException("QYL_COST_SYNC_INTERVAL_MINUTES must be between 1 and 1440.");

        var lookbackDays = int.Parse(configuration["QYL_COST_LOOKBACK_DAYS"] ?? "31", CultureInfo.InvariantCulture);
        if (lookbackDays is < 1 or > 180)
            throw new InvalidOperationException("QYL_COST_LOOKBACK_DAYS must be between 1 and 180.");

        return new ProviderCostSyncOptions
        {
            ProjectId = projectId,
            SyncInterval = TimeSpan.FromMinutes(intervalMinutes),
            LookbackDays = lookbackDays,
            OpenAiAdminKey = configuration["QYL_OPENAI_ADMIN_KEY"],
            OpenAiProjectId = ReadScope(configuration, "QYL_OPENAI_PROJECT_ID"),
            AnthropicAdminKey = configuration["QYL_ANTHROPIC_ADMIN_KEY"],
            AnthropicWorkspaceScope = ReadAnthropicScope(configuration)
        };
    }

    private static ProviderCostScope ReadAnthropicScope(IConfiguration configuration)
    {
        var value = ReadScope(configuration, "QYL_ANTHROPIC_WORKSPACE_ID");
        if (value is null) return ProviderCostScope.Organization;
        return string.Equals(value, "default", StringComparison.OrdinalIgnoreCase)
            ? ProviderCostScope.DefaultWorkspace
            : ProviderCostScope.ForIdentifier(value);
    }

    private static string? ReadScope(IConfiguration configuration, string name)
    {
        var value = configuration[name];
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 256 || value.IndexOfAny(['\r', '\n']) >= 0)
            throw new InvalidOperationException($"{name} must be a single identifier of at most 256 characters.");
        return value;
    }
}
