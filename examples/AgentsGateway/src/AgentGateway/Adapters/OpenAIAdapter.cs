using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.ClientModel;
using AgentGateway.Core;

namespace AgentGateway.Adapters;

[ModelProvider("openai", "OpenAI", ProviderCapabilities.Chat | ProviderCapabilities.Tools | ProviderCapabilities.StructuredOutputs, "apiKey")]
public sealed class OpenAIAdapter : IChatClient, IModelCatalog
{
    private readonly IChatClient _inner;
    private readonly string _defaultModel;

    public OpenAIAdapter(IConfiguration cfg)
    {
        var key = cfg["OPENAI_KEY"] ?? throw new InvalidOperationException("Missing configuration: OPENAI_KEY");
        _defaultModel = cfg["OPENAI_MODEL"] ?? "gpt-4o-mini";

        var openAi = new OpenAI.Chat.ChatClient(_defaultModel, new ApiKeyCredential(key));
        _inner = openAi.AsIChatClient();
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => _inner.GetResponseAsync(chatMessages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => _inner.GetStreamingResponseAsync(chatMessages, options, cancellationToken);

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ModelInfo>>(new[]
        {
            new ModelInfo(_defaultModel, ProviderCapabilities.Chat | ProviderCapabilities.Tools | ProviderCapabilities.StructuredOutputs, new Dictionary<string, string>())
        });
    }

    public void Dispose() => _inner.Dispose();
    public object? GetService(Type serviceType, object? serviceKey = null) => _inner.GetService(serviceType, serviceKey);
}
