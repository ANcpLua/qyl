// Probe file for CodeRabbit configuration validation. Not referenced by any
// project; this PR exists to exercise the review pipeline and will be closed.
using System.Net.Http;
using System.Threading.Tasks;

namespace Qyl.Probe;

public sealed class TelemetryForwarder
{
    private readonly HttpClient _client = new();

    // Violation: hardcoded endpoint instead of IOptions/IConfiguration.
    private const string Endpoint = "http://localhost:4318/v1/traces";

    // Violation: public async method without CancellationToken.
    public async Task<string> FetchAsync()
    {
        return await _client.GetStringAsync(Endpoint);
    }

    // Violation: sync-over-async.
    public string FetchBlocking()
    {
        return _client.GetStringAsync(Endpoint).Result;
    }
}
