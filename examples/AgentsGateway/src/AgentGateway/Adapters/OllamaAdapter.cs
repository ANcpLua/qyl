using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentGateway.Core;
using Microsoft.Extensions.AI;

namespace AgentGateway.Adapters;

[ModelProvider("ollama", "Ollama", ProviderCapabilities.Chat | ProviderCapabilities.Streaming, "none")]
public sealed class OllamaAdapter : IChatClient, IModelCatalog
{
    private readonly string _defaultModel;
    private readonly HttpClient _http;

    public OllamaAdapter(IConfiguration cfg)
    {
        var baseUrl = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _defaultModel = cfg["Ollama:DefaultModel"] ?? "llama3.2";
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options, false);
        var response = await _http.PostAsJsonAsync("/api/chat", request, OllamaJsonContext.Default.OllamaChatRequest,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync(OllamaJsonContext.Default.OllamaChatResponse, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, result?.Message?.Content is { } content ? content : string.Empty));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options, true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat");
        httpRequest.Content = JsonContent.Create(request, OllamaJsonContext.Default.OllamaChatRequest);

        using var response =
            await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = JsonSerializer.Deserialize(line, OllamaJsonContext.Default.OllamaChatResponse);
            if (chunk?.Message?.Content is { Length: > 0 } content)
                yield return new ChatResponseUpdate(ChatRole.Assistant, content);

            if (chunk?.Done == true) break;
        }
    }

    public void Dispose() => _http.Dispose();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetFromJsonAsync("/api/tags", OllamaJsonContext.Default.OllamaTagsResponse, ct);
            if (response?.Models is not { } models)
                return [];

            return models
                .Select(m => new ModelInfo(
                    m.Name ?? "unknown",
                    ProviderCapabilities.Chat | ProviderCapabilities.Streaming,
                    new Dictionary<string, string>
                    {
                        ["size"] = m.Size.ToString(CultureInfo.InvariantCulture),
                        ["modified_at"] = m.ModifiedAt ?? string.Empty
                    }))
                .ToArray();
        }
        catch (HttpRequestException)
        {
            // Ollama not running - return empty list
            return [];
        }
    }

    private OllamaChatRequest BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
    {
        return new OllamaChatRequest
        {
            Model = options?.ModelId ?? _defaultModel,
            Messages =
            [
                .. messages.Select(m => new OllamaMessage
                {
                    Role = m.Role.Value,
                    Content = m.Text
                })
            ],
            Stream = stream,
            Options = options?.Temperature is { } temp ? new OllamaOptions { Temperature = temp } : null
        };
    }
}

// Request/Response models
internal sealed class OllamaChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")] public List<OllamaMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")] public bool Stream { get; set; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OllamaOptions? Options { get; set; }
}

internal sealed class OllamaMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}

internal sealed class OllamaOptions
{
    [JsonPropertyName("temperature")] public float Temperature { get; set; }
}

internal sealed class OllamaChatResponse
{
    [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }

    [JsonPropertyName("done")] public bool Done { get; set; }
}

internal sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")] public List<OllamaModelInfo>? Models { get; set; }
}

internal sealed class OllamaModelInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("modified_at")] public string? ModifiedAt { get; set; }
}

// AOT-compatible JSON context
[JsonSerializable(typeof(OllamaChatRequest))]
[JsonSerializable(typeof(OllamaChatResponse))]
[JsonSerializable(typeof(OllamaTagsResponse))]
internal sealed partial class OllamaJsonContext : JsonSerializerContext
{
}