
using System.ClientModel;
using AgentGateway.Core;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentGateway.Adapters;

[ModelProvider("openai", "OpenAI",
    ProviderCapabilities.Chat | ProviderCapabilities.Tools | ProviderCapabilities.StructuredOutputs, "apiKey")]
public sealed class OpenAIAdapter : IChatClient, IModelCatalog
{
    private readonly string _defaultModel;
    private readonly IChatClient _inner;

    public OpenAIAdapter(IConfiguration cfg)
    {
        var key = cfg["OPENAI_KEY"] ?? throw new InvalidOperationException("Missing configuration: OPENAI_KEY");
        _defaultModel = cfg["OPENAI_MODEL"] ?? "gpt-4o-mini";

        var openAi = new ChatClient(_defaultModel, new ApiKeyCredential(key));
        _inner = openAi.AsIChatClient();
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetResponseAsync(messages, options, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return _inner.GetStreamingResponseAsync(messages, options, cancellationToken);
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
                ProviderCapabilities.Chat | ProviderCapabilities.Tools | ProviderCapabilities.StructuredOutputs,
                new Dictionary<string, string>())
        });
    }
}