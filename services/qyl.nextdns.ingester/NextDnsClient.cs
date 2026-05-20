using System.Net.Http.Json;

namespace Qyl.NextDns.Ingester;

internal sealed class NextDnsClient(HttpClient http, IngesterOptions options)
{
    public async Task<NextDnsLogPage?> FetchLogsAsync(string? cursor, CancellationToken cancellationToken)
    {
        var path = $"/profiles/{Uri.EscapeDataString(options.ProfileId)}/logs";
        if (!string.IsNullOrWhiteSpace(cursor))
            path += $"?cursor={Uri.EscapeDataString(cursor)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", options.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync(IngesterJsonContext.Default.NextDnsLogPage, cancellationToken)
            .ConfigureAwait(false);
    }
}
