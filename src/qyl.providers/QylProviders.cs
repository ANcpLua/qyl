// =============================================================================
// qyl.providers - Unified AI Provider Extensions
// Single entry point for all AI backends: Gemini, OpenAI, Ollama
// =============================================================================

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Microsoft;
using OllamaSharp;
using OpenAI;
using Qyl;

namespace qyl.providers;

/// <summary>
/// Unified factory for creating AI chat clients across different providers.
/// </summary>
public static class QylProviders
{
    // =========================================================================
    // GEMINI (Google AI)
    // =========================================================================

    /// <summary>
    /// Creates a Gemini IChatClient using Google AI API.
    /// </summary>
    /// <param name="apiKey">Google AI API key (from GOOGLE_GENAI_API_KEY env var or AI Studio).</param>
    /// <param name="model">Model name (default: gemini-2.5-flash).</param>
    public static IChatClient Gemini(string apiKey, string model = "gemini-2.5-flash")
    {
        Throw.IfNullOrWhiteSpace(apiKey);
        Throw.IfNullOrWhiteSpace(model);
        return new GeminiChatClient(apiKey: apiKey, model: model, logger: null);
    }

    /// <summary>
    /// Creates a Gemini IChatClient from environment variables.
    /// </summary>
    /// <remarks>
    /// Reads GOOGLE_GENAI_API_KEY and optionally GOOGLE_GENAI_MODEL from environment.
    /// </remarks>
    public static IChatClient GeminiFromEnv(string modelEnvVar = "GOOGLE_GENAI_MODEL", string defaultModel = "gemini-2.5-flash")
    {
        var apiKey = Environment.GetEnvironmentVariable("GOOGLE_GENAI_API_KEY")
            ?? throw new InvalidOperationException("GOOGLE_GENAI_API_KEY environment variable is not set.");
        var model = Environment.GetEnvironmentVariable(modelEnvVar) ?? defaultModel;
        return Gemini(apiKey, model);
    }

    // =========================================================================
    // OPENAI
    // =========================================================================

    /// <summary>
    /// Creates an OpenAI IChatClient.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <param name="model">Model name (default: gpt-4o-mini).</param>
    public static IChatClient OpenAI(string apiKey, string model = "gpt-4o-mini")
    {
        Throw.IfNullOrWhiteSpace(apiKey);
        Throw.IfNullOrWhiteSpace(model);
        return new OpenAIClient(apiKey)
            .GetChatClient(model)
            .AsIChatClient();
    }

    /// <summary>
    /// Creates an OpenAI IChatClient from environment variables.
    /// </summary>
    /// <remarks>
    /// Reads OPENAI_APIKEY and optionally OPENAI_MODEL from environment.
    /// </remarks>
    public static IChatClient OpenAIFromEnv(string modelEnvVar = "OPENAI_MODEL", string defaultModel = "gpt-4o-mini")
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_APIKEY")
            ?? throw new InvalidOperationException("OPENAI_APIKEY environment variable is not set.");
        var model = Environment.GetEnvironmentVariable(modelEnvVar) ?? defaultModel;
        return OpenAI(apiKey, model);
    }

    // =========================================================================
    // OLLAMA (Local)
    // =========================================================================

    /// <summary>
    /// Creates an Ollama IChatClient for local LLM inference.
    /// </summary>
    /// <param name="endpoint">Ollama server URL (e.g., http://localhost:11434).</param>
    /// <param name="model">Model name (e.g., llama3, mistral, codellama).</param>
    public static IChatClient Ollama(string endpoint, string model)
    {
        Throw.IfNullOrWhiteSpace(endpoint);
        Throw.IfNullOrWhiteSpace(model);
        // OllamaApiClient implements IChatClient directly
        return new OllamaApiClient(new Uri(endpoint), model);
    }

    /// <summary>
    /// Creates an Ollama IChatClient from environment variables.
    /// </summary>
    /// <remarks>
    /// Reads OLLAMA_ENDPOINT and OLLAMA_MODEL_NAME from environment.
    /// </remarks>
    public static IChatClient OllamaFromEnv()
    {
        var endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
            ?? throw new InvalidOperationException("OLLAMA_ENDPOINT environment variable is not set.");
        var model = Environment.GetEnvironmentVariable("OLLAMA_MODEL_NAME")
            ?? throw new InvalidOperationException("OLLAMA_MODEL_NAME environment variable is not set.");
        return Ollama(endpoint, model);
    }
}

// =============================================================================
// Extension Methods for Fluent Agent Creation
// =============================================================================

/// <summary>
/// Extensions for creating AIAgents from IChatClient instances.
/// </summary>
public static class QylAgentExtensions
{
    /// <summary>
    /// Creates an AIAgent from any IChatClient with the specified configuration.
    /// </summary>
    /// <param name="client">The chat client to wrap.</param>
    /// <param name="name">Agent name for identification.</param>
    /// <param name="instructions">System instructions/prompt for the agent.</param>
    /// <param name="tools">Optional tools the agent can use.</param>
    public static ChatClientAgent AsAgent(
        this IChatClient client,
        string? name = null,
        string? instructions = null,
        IList<AITool>? tools = null)
    {
        Throw.IfNull(client);
        return client.CreateAIAgent(
            name: name,
            instructions: instructions,
            tools: tools);
    }
}
