using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;
using AgentGateway.Core;

namespace AgentGateway.Adapters;

[ModelProvider("github", "GitHub Models", ProviderCapabilities.Chat | ProviderCapabilities.Tools | ProviderCapabilities.Streaming, "githubToken")]
public sealed class GitHubAdapter : IChatClient, IModelCatalog
{
    private readonly IChatClient _inner;
    private readonly string _defaultModel;

    public GitHubAdapter(IConfiguration cfg)
    {
        var token = cfg["GITHUB_TOKEN"] ?? throw new InvalidOperationException("Missing configuration: GITHUB_TOKEN");
        _defaultModel = cfg["GITHUB_MODEL"] ?? "gpt-4o-mini";

        var client = new OpenAI.Chat.ChatClient(
            _defaultModel,
            new ApiKeyCredential(token),
            new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://models.inference.ai.azure.com") });

        _inner = client.AsIChatClient();
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => _inner.GetResponseAsync(chatMessages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => _inner.GetStreamingResponseAsync(chatMessages, options, cancellationToken);

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ModelInfo>>(new[]
        {
            new ModelInfo(_defaultModel, ProviderCapabilities.Chat | ProviderCapabilities.Tools | ProviderCapabilities.Streaming, new Dictionary<string, string>())
        });
    }

    public void Dispose() => _inner.Dispose();
    public object? GetService(Type serviceType, object? serviceKey = null) => _inner.GetService(serviceType, serviceKey);
}
