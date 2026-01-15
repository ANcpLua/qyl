using Microsoft.Extensions.AI;
using Polly;
using Polly.Retry;

namespace AgentGateway.Core;

public interface IProviderSelectionPolicy
{
    (string providerId, string? modelId) SelectProvider(ChatOptions? options, HttpContext? http, IConfiguration cfg);
}

public sealed class HeaderSelectionPolicy : IProviderSelectionPolicy
{
    public (string providerId, string? modelId) SelectProvider(ChatOptions? options, HttpContext? http, IConfiguration cfg)
    {
        var provider = http?.Request.Headers["X-Provider"].ToString();
        var model = http?.Request.Headers["X-Model"].ToString();

        if (!string.IsNullOrWhiteSpace(provider)) return (provider, string.IsNullOrWhiteSpace(model) ? null : model);

        var def = cfg["Providers:DefaultProvider"] ?? "openai";
        return (def, null);
    }
}

public sealed class ProviderRouterChatClient : IChatClient
{
    private readonly IConfiguration _cfg;
    private readonly IHttpContextAccessor _http;
    private readonly ResiliencePipeline _pipeline;
    private readonly IProviderSelectionPolicy _policy;
    private readonly IProviderRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    public ProviderRouterChatClient(
        IProviderRegistry registry,
        IProviderSelectionPolicy policy,
        IServiceProvider serviceProvider,
        IHttpContextAccessor http,
        IConfiguration cfg)
    {
        _registry = registry;
        _policy = policy;
        _serviceProvider = serviceProvider;
        _http = http;
        _cfg = cfg;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddTimeout(TimeSpan.FromSeconds(60))
            .Build();
    }

    public void Dispose()
    {
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _pipeline.ExecuteAsync(async ct =>
        {
                        var (providerId, modelId) = _policy.SelectProvider(options, _http.HttpContext, _cfg);
            if (modelId != null)
            {
                options ??= new ChatOptions();
                options.ModelId = modelId;
            }

            var client = _registry.Resolve(providerId, _serviceProvider);
            return await client.GetResponseAsync(messages, options, ct);
        }, cancellationToken).AsTask();
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
                    var (providerId, modelId) = _policy.SelectProvider(options, _http.HttpContext, _cfg);
        if (modelId != null)
        {
            options ??= new ChatOptions();
            options.ModelId = modelId;
        }

        var client = _registry.Resolve(providerId, _serviceProvider);
        return client.GetStreamingResponseAsync(messages, options, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IProviderRegistry)) return _registry;
        if (serviceType == typeof(ResiliencePipeline)) return _pipeline;
        return null;
    }
}