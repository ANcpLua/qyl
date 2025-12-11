using Microsoft.Extensions.AI;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Microsoft;
using qyl.Shared;

namespace qyl.providers.gemini;

public static class GeminiChatClientExtensions
{
    public static GoogleAI CreateGoogleAI(string apiKey)
    {
        Throw.IfNullOrWhitespace(apiKey);
        return new GoogleAI(apiKey);
    }

    public static GenerativeModel CreateModel(this GoogleAI googleAI, string modelId = "gemini-2.5-flash")
    {
        Throw.IfNull(googleAI);
        Throw.IfNullOrWhitespace(modelId);
        return googleAI.GenerativeModel(modelId);
    }

#pragma warning disable CA2000 // Dispose objects before losing scope - caller owns lifecycle
    public static IChatClient CreateGeminiChatClient(string apiKey, string modelId = "gemini-2.5-flash") =>
        CreateGoogleAI(apiKey).CreateModel(modelId).AsIChatClient();
#pragma warning restore CA2000
}
