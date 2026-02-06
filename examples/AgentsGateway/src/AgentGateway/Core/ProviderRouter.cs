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

public sealed class ProviderRouterChatClient(
    IProviderRegistry registry,
    IProviderSelectionPolicy policy,
    IServiceProvider serviceProvider,
    IHttpContextAccessor http,
    IConfiguration cfg)
    : IChatClient
{
    private readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        })
        .AddTimeout(TimeSpan.FromSeconds(60))
        .Build();

    public void Dispose()
    {
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _pipeline.ExecuteAsync(async ct =>
        {
            var (providerId, modelId) = policy.SelectProvider(options, http.HttpContext, cfg);
            if (modelId is not null)
            {
                options ??= new ChatOptions();
                options.ModelId = modelId;
            }

            var client = registry.Resolve(providerId, serviceProvider);
            return await client.GetResponseAsync(messages, options, ct);
        }, cancellationToken).AsTask();
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var (providerId, modelId) = policy.SelectProvider(options, http.HttpContext, cfg);
        if (modelId is not null)
        {
            options ??= new ChatOptions();
            options.ModelId = modelId;
        }

        var client = registry.Resolve(providerId, serviceProvider);
        return client.GetStreamingResponseAsync(messages, options, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IProviderRegistry)) return registry;
        if (serviceType == typeof(ResiliencePipeline)) return _pipeline;
        return null;
    }
}