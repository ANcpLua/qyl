using System.ClientModel;
using AgentGateway.Core;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentGateway.Adapters;

[ModelProvider("github", "GitHub Models",
    ProviderCapabilities.Chat | ProviderCapabilities.Tools | ProviderCapabilities.Streaming, "githubToken")]
public sealed class GitHubAdapter : IChatClient, IModelCatalog
{
    private readonly string _defaultModel;
    private readonly IChatClient _inner;

    public GitHubAdapter(IConfiguration cfg)
    {
        var token = cfg["GITHUB_TOKEN"] ?? throw new InvalidOperationException("Missing configuration: GITHUB_TOKEN");
        _defaultModel = cfg["GITHUB_MODEL"] ?? "gpt-4o-mini";

        var client = new ChatClient(
            _defaultModel,
            new ApiKeyCredential(token),
            new OpenAIClientOptions { Endpoint = new Uri("https://models.inference.ai.azure.com") });

        _inner = client.AsIChatClient();
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetResponseAsync(chatMessages, options, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return _inner.GetStreamingResponseAsync(chatMessages, options, cancellationToken);
    }

    public void Dispose()
    {
        _inner.Dispose();
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return _inner.GetService(serviceType, serviceKey);
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ModelInfo>>(new[]
        {
            new ModelInfo(_defaultModel,
                ProviderCapabilities.Chat | ProviderCapabilities.Tools | ProviderCapabilities.Streaming,
                new Dictionary<string, string>())
        });
    }
}