using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace qyl.mcp.Agents;

/// <summary>
///     Creates an <see cref="IChatClient"/> for the use_qyl meta-agent from environment variables.
///     Uses the OpenAI .NET SDK which supports OpenAI-compatible endpoints (Ollama, Anthropic, etc.).
/// </summary>
internal static class AgentLlmFactory
{
    /// <summary>
    ///     Attempts to create an IChatClient from configuration.
    ///     Returns null if QYL_AGENT_API_KEY is not set (agent not configured).
    /// </summary>
    /// <remarks>
    ///     Environment variables:
    ///     - QYL_AGENT_API_KEY — LLM API key (required)
    ///     - QYL_AGENT_MODEL — Model name, default "gpt-4o"
    ///     - QYL_AGENT_ENDPOINT — OpenAI-compatible endpoint URL (optional, for Ollama/Anthropic/etc.)
    /// </remarks>
    public static IChatClient? TryCreate(IConfiguration config)
    {
        var apiKey = config["QYL_AGENT_API_KEY"];
        if (string.IsNullOrEmpty(apiKey))
            return null;

        var model = config["QYL_AGENT_MODEL"] ?? "gpt-4o";
        var endpoint = config["QYL_AGENT_ENDPOINT"];

        OpenAIClient openAiClient;
        if (endpoint is not null)
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
            openAiClient = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
        }
        else
        {
            openAiClient = new OpenAIClient(apiKey);
        }

        return openAiClient.GetChatClient(model).AsIChatClient();
    }
}
