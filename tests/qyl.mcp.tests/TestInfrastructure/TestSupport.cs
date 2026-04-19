namespace Qyl.Mcp.Tests.TestInfrastructure;

internal static class TestCollectorEndpoint
{
    public static Uri BaseAddress { get; } = new UriBuilder(Uri.UriSchemeHttps, "collector.internal").Uri;

    public static string Url => BaseAddress.ToString().TrimEnd('/');

    public static string Path(string relativePath)
    {
        var builder = new UriBuilder(BaseAddress) { Path = relativePath };
        return builder.Uri.ToString();
    }
}

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Responder { get; set; } =
        static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public AuthenticationHeaderValue? LastAuthorization { get; private set; }
    public string? LastApiKey { get; private set; }
    public string LastRequestBody { get; private set; } = string.Empty;
    public string LastRequestUri { get; private set; } = string.Empty;
    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastAuthorization = request.Headers.Authorization;
        LastApiKey = request.Headers.TryGetValues(McpAuthOptions.HeaderName, out var values)
            ? values.SingleOrDefault()
            : null;
        LastRequestUri = request.RequestUri?.ToString() ?? string.Empty;
        LastRequestBody = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return await Responder(request, cancellationToken).ConfigureAwait(false);
    }
}

internal static class ScopeFactory
{
    public static QylScope Create(string? serviceName = null, string? sessionId = null) =>
        QylScope.ForTest(serviceName, sessionId);
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal static class TestServiceCollectionFactory
{
    public static ServiceCollection Create(
        RecordingHttpMessageHandler? handler = null,
        string? collectorUrl = null,
        string? apiKey = null,
        string? serviceName = null,
        string? sessionId = null,
        TimeSpan? timeout = null)
    {
        ServiceCollection services = [];
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(apiKey is null
                ? []
                : new Dictionary<string, string?> { [$"{McpAuthOptions.SectionName}:Token"] = apiKey })
            .Build();

        services.AddLogging();
        services.AddRedaction();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(ScopeFactory.Create(serviceName, sessionId));
        services.AddMcpAuth(configuration);

        var builder = services.AddCollectorHttpClient(collectorUrl ?? TestCollectorEndpoint.Url, timeout);
        if (handler is not null)
            builder.ConfigurePrimaryHttpMessageHandler(() => handler);

        return services;
    }
}
